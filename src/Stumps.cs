using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Regrove
{
    // Stumps turn back into trees: a sapling when a player removes one (or, with RemoveOnFell, as soon
    // as its tree falls), and a sapling or the tree itself when one is left standing for StumpRegrowDays.
    // All on the server, with plain game objects, so players need no mod.
    internal static class Stumps
    {
        internal static readonly int SeenHash = "regrove_seen".GetStableHashCode();   // when the sweep first saw a stump
        private static readonly int TreeHash = "regrove_tree".GetStableHashCode();   // the tree that left a stump
        private static readonly int PlantedHash = "regrove_planted".GetStableHashCode();   // a sapling Regrove made
        private static readonly int OursHash = "HasFieldsPlant".GetStableHashCode();       // ...and how 1.0.0 told them apart
        private const int SWEEP_BUDGET = 2000;          // objects looked at per frame
        private const float FALL_WATCH = 15f;           // seconds a felled tree's stump is looked for

        private struct Pending { public float at; public int stump, tree; public Vector3 pos; public Quaternion rot; }
        private struct Felled { public float until, next; public int tree, stump; public Vector3 pos; }
        private struct Removal { public float at; public ZDOID id; public int tree; }
        private static readonly List<Pending> pending = new List<Pending>();
        private static readonly List<Felled> felled = new List<Felled>();
        private static readonly List<Removal> removals = new List<Removal>();
        private static readonly HashSet<ZDOID> ours = new HashSet<ZDOID>();   // stumps Regrove removes itself
        // stumps whose tree we saw fall, until the sweep can write it on the stump (a player owns it at first)
        private static readonly Dictionary<ZDOID, int> treeOf = new Dictionary<ZDOID, int>();
        private static readonly List<ZDO> near = new List<ZDO>();
        private static readonly List<ZDOID> due = new List<ZDOID>();
        private static int sector = -1, index, seen, stamped, busy, growing, sweeps;
        // what Regrove costs the server: per round, its time on the main thread and its slowest frame
        private static readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
        private static double roundMs, slowestMs;
        private static int roundFrames;
        private static float nextSweep = 60f;

        private static bool Active => Plugin.Enabled.Value && !Plugin.blocked
            && ZNet.instance != null && ZNet.instance.IsServer() && ZDOMan.instance != null && ZNetScene.instance != null;

        // The server hears of every removed object here, players' and its own, while the object is still known
        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.HandleDestroyedZDO))]
        private static class Destroyed
        {
            private static void Prefix(ZDOMan __instance, ZDOID uid)
            {
                if (!Active || !Trees.Ready || ours.Remove(uid)) return;
                // a client sent back an object that was already gone: the game removes it again
                if (__instance.m_deadZDOs.ContainsKey(uid)) return;
                var zdo = __instance.GetZDO(uid);
                if (zdo == null) return;
                int prefab = zdo.GetPrefab();
                Wood.Broken(zdo);
                // a tree fell: its stump turns up next to it in a moment
                if (Trees.StumpOf.TryGetValue(prefab, out int stump))
                {
                    if (Trees.Excluded(prefab)) return;
                    felled.Add(new Felled { until = Time.time + FALL_WATCH, tree = prefab, stump = stump, pos = zdo.GetPosition() });
                    return;
                }
                if (!Trees.Stumps.ContainsKey(prefab)) return;
                if (!treeOf.TryGetValue(uid, out int tree)) tree = zdo.GetInt(TreeHash);
                treeOf.Remove(uid);
                if (Random.Range(0, 100) >= Plugin.SaplingChance.Value) return;
                pending.Add(new Pending { at = Time.time + Plugin.SaplingDelay.Value, stump = prefab, tree = tree, pos = zdo.GetPosition(), rot = zdo.GetRotation() });
            }
        }

        public static void Update()
        {
            if (!Active) return;
            clock.Restart();
            Tick();
            double ms = clock.Elapsed.TotalMilliseconds;
            roundMs += ms; roundFrames++;
            if (ms > slowestMs) slowestMs = ms;
        }

        private static void Tick()
        {
            if (!Trees.Ready)
            {
                Trees.Load();
                if (!Trees.Ready) return;
                if (EnvMan.instance != null) ZLog.Log($"Regrove: an in-game day is {EnvMan.instance.m_dayLengthSec / 60} minutes");
            }
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                if (Time.time < p.at) continue;
                pending.RemoveAt(i);
                int tree = TreeFor(p.stump, p.tree, p.pos);
                if (!Trees.Excluded(tree) && !Protected(p.pos)) Sow(tree, p.pos, p.rot);
            }
            Wood.Update();
            Signs.Update();
            WatchFelled();
            RemoveStumps();
            Sweep();
        }

        // Find the stump a felled tree left, remember which tree it was, and with RemoveOnFell take the
        // stump away and plant the sapling at once
        private static void WatchFelled()
        {
            for (int i = felled.Count - 1; i >= 0; i--)
            {
                var f = felled[i];
                if (Time.time > f.until) { felled.RemoveAt(i); continue; }
                if (Time.time < f.next) continue;
                f.next = Time.time + 0.5f;
                felled[i] = f;
                FindNear(f.pos);
                foreach (var zdo in near)
                {
                    if (zdo.GetPrefab() != f.stump || Utils.DistanceXZ(zdo.GetPosition(), f.pos) > 0.5f || treeOf.ContainsKey(zdo.m_uid)) continue;
                    treeOf[zdo.m_uid] = f.tree;
                    if (Plugin.RemoveOnFell.Value)
                        removals.Add(new Removal { at = Time.time + Plugin.SaplingDelay.Value, id = zdo.m_uid, tree = f.tree });
                    felled.RemoveAt(i);
                    break;
                }
            }
        }

        private static void RemoveStumps()
        {
            for (int i = removals.Count - 1; i >= 0; i--)
            {
                var r = removals[i];
                if (Time.time < r.at) continue;
                removals.RemoveAt(i);
                var zdo = ZDOMan.instance.GetZDO(r.id);
                if (zdo == null) continue;   // dug out meanwhile: the usual sapling is on its way
                var pos = zdo.GetPosition();
                if (!Protected(pos) && Random.Range(0, 100) < Plugin.SaplingChance.Value) Sow(r.tree, pos, zdo.GetRotation());
                Wood.StumpDrops(zdo.GetPrefab(), pos);
                Remove(zdo);
            }
        }

        // Left-standing stumps, a slice of the world each frame. Each gets a first-seen time; once it
        // is old enough and no player is near it, it becomes a sapling or its tree again.
        private static void Sweep()
        {
            if (Plugin.RegrowDays.Value <= 0f) return;
            var bySector = ZDOMan.instance.m_objectsBySector;
            if (bySector == null) return;
            if (sector < 0)
            {
                if (Time.time < nextSweep) return;
                sector = 0; index = 0; seen = stamped = busy = growing = 0; due.Clear();
                roundMs = slowestMs = 0; roundFrames = 0;
            }
            long now = ZNet.instance.GetTime().Ticks;
            long age = RegrowAge();
            int budget = SWEEP_BUDGET;
            // a slice each frame; a crowded sector is split across frames too
            while (sector < bySector.Length && budget > 0)
            {
                var list = bySector[sector];
                if (list == null || index >= list.Count) { sector++; index = 0; continue; }
                int end = Mathf.Min(list.Count, index + budget);
                budget -= end - index;
                for (; index < end; index++)
                {
                    var zdo = list[index];
                    int prefab = zdo.GetPrefab();
                    if (Trees.Saplings.Contains(prefab) && IsOurs(zdo)) growing++;
                    if (!Trees.Stumps.ContainsKey(prefab)) continue;
                    seen++;
                    if (Busy(zdo)) { busy++; continue; }
                    if (treeOf.TryGetValue(zdo.m_uid, out int tree)) { zdo.Set(TreeHash, tree); treeOf.Remove(zdo.m_uid); }
                    long first = zdo.GetLong(SeenHash);
                    if (first == 0L) { zdo.Set(SeenHash, now); stamped++; }
                    else if (now - first >= age && due.Count < Plugin.MaxPerRound.Value && WillRegrow(zdo)) due.Add(zdo.m_uid);
                }
            }
            if (sector < bySector.Length) return;
            sector = -1;
            nextSweep = Time.time + Plugin.RoundMinutes.Value * 60f;
            bool asTree = Plugin.RegrowAs.Value == Plugin.Regrowth.Tree;
            int grown = 0, kept = 0;
            foreach (var id in due)
            {
                var zdo = ZDOMan.instance.GetZDO(id);
                if (zdo == null || Busy(zdo)) continue;
                var pos = zdo.GetPosition();
                int tree = TreeFor(zdo.GetPrefab(), zdo.GetInt(TreeHash), pos);
                bool done = !Trees.Excluded(tree) && !Protected(pos) && (asTree ? Grow(tree, zdo) : Sow(tree, pos, zdo.GetRotation()));
                // kept, left alone, or no sapling can grow there: ask again in another StumpRegrowDays
                if (!done) { zdo.Set(SeenHash, now); kept++; continue; }
                Remove(zdo);
                grown++;
            }
            if (due.Count > 0 || sweeps++ == 0)
                ZLog.Log($"Regrove: {seen} stumps standing ({stamped} new, {busy} near players), {grown} turned into {(asTree ? "trees" : "saplings")}, {kept} kept (near a base or path, left alone, or nothing grows there); {growing} saplings regrowing; round took {roundMs:0} ms over {roundFrames} frames, slowest frame {slowestMs:0.00} ms");
            due.Clear();
        }

        // a player has it loaded: leave it to the game there
        private static bool Busy(ZDO zdo)
        {
            if (!zdo.HasOwner()) return false;
            long owner = zdo.GetOwner();
            return owner != ZDOMan.GetSessionID() && ZNet.instance.GetPeer(owner) != null;
        }

        // RegrowChance, rolled once per stump: its id decides, so the same stump always gets the same
        // answer (rolling again every round would let every stump through in the end)
        internal static bool WillRegrow(ZDO stump)
        {
            int chance = Plugin.RegrowChance.Value;
            if (chance >= 100) return true;
            uint h = unchecked((uint)stump.m_uid.ID * 2654435761u ^ (uint)stump.m_uid.UserID * 40503u);
            return h % 100 < chance;
        }

        // StumpRegrowDays in the game's ticks
        internal static long RegrowAge()
        {
            long day = (EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800L) * System.TimeSpan.TicksPerSecond;
            return (long)(Plugin.RegrowDays.Value * day);
        }

        internal static bool IsOurs(ZDO sapling) => sapling.GetBool(PlantedHash) || sapling.GetBool(OursHash);

        internal static int TreeOf(ZDO stump)
        {
            if (!treeOf.TryGetValue(stump.m_uid, out int known)) known = stump.GetInt(TreeHash);
            return TreeFor(stump.GetPrefab(), known, stump.GetPosition());
        }

        // the game's own way to remove an object it doesn't own: take it over first
        internal static void Remove(ZDO zdo)
        {
            ours.Add(zdo.m_uid);
            treeOf.Remove(zdo.m_uid);
            zdo.SetOwner(ZDOMan.GetSessionID());
            ZDOMan.instance.DestroyZDO(zdo);
        }

        private static void FindNear(Vector3 pos)
        {
            near.Clear();
            // the zone around pos and the 8 next to it: 64 m or more in every direction
            ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(pos), new SimulationDistance(1, 0, classic: true), near);
        }

        internal static bool Protected(Vector3 pos)
        {
            if (Plugin.ProtectGround.Value && Ground.Worked(pos)) return true;
            bool wards = Plugin.ProtectWards.Value;
            float radius = Plugin.ProtectRadius.Value;
            if (!wards && radius <= 0f) return false;
            FindNear(pos);
            foreach (var zdo in near)
            {
                int prefab = zdo.GetPrefab();
                float d = Utils.DistanceXZ(zdo.GetPosition(), pos);
                if (wards && Trees.WardRadius.TryGetValue(prefab, out float r) && d <= r && zdo.GetBool(ZDOVars.s_enabled)) return true;
                if (radius > 0f && d <= radius && Trees.Pieces.Contains(prefab) && zdo.GetLong(ZDOVars.s_creator) != 0L) return true;
            }
            return false;
        }

        // Which tree left this stump: the one we saw fall, else the only tree that leaves it, else the
        // most common of those trees around it
        private static int TreeFor(int stump, int known, Vector3 pos)
        {
            var trees = Trees.Stumps[stump];
            if (known != 0 && trees.Contains(known)) return known;
            if (trees.Count == 1) return trees[0];
            FindNear(pos);
            var counts = new Dictionary<int, int>();
            int best = trees[0], bestCount = 0;
            foreach (var zdo in near)
            {
                int prefab = zdo.GetPrefab();
                if (!trees.Contains(prefab)) continue;
                counts.TryGetValue(prefab, out int c);
                counts[prefab] = ++c;
                if (c > bestCount) { best = prefab; bestCount = c; }
            }
            return best;
        }

        // A new sapling, made the way the game makes one, with a few of its settings changed for this
        // one object (the game's own per-object overrides, which every client applies when it loads it)
        private static bool Sow(int tree, Vector3 pos, Quaternion rot)
        {
            if (!Trees.SaplingOf.TryGetValue(tree, out int hash)) return false;   // swamp, Mistlands and Ashlands trees
            var prefab = ZNetScene.instance.GetPrefab(hash);
            var plant = prefab != null ? prefab.GetComponentInChildren<Plant>(true) : null;
            if (plant == null) return false;
            // a sapling outside its biomes never grows: leave the ground bare there
            if (WorldGenerator.instance != null && (WorldGenerator.instance.GetBiome(pos) & plant.m_biome) == 0) return false;
            var zdo = Create(hash, pos, rot);
            if (zdo == null) return false;
            zdo.Set(ZDOVars.s_plantTime, ZNet.instance.GetTime().Ticks);
            zdo.Set(ZDOVars.s_seed, (int)(zdo.m_uid.ID + zdo.m_uid.UserID));   // the game's own default, set now so the countdown knows it
            zdo.Set(PlantedHash, true);   // the server's own note; players' games never read it
            // only the settings that differ from the sapling's own: each one is work for every
            // player's game that loads the sapling
            float m = Mathf.Max(0f, Plugin.GrowTime.Value);
            bool any = false;
            if (plant.m_growRadius != 0f) { zdo.Set("Plant.m_growRadius", 0f); any = true; }   // grows where the old tree stood, among the others
            if (plant.m_needCultivatedGround) { zdo.Set("Plant.m_needCultivatedGround", false); any = true; }
            if (plant.m_destroyIfCantGrow) { zdo.Set("Plant.m_destroyIfCantGrow", false); any = true; }
            if (!plant.m_tolerateCold) { zdo.Set("Plant.m_tolerateCold", true); any = true; }   // the mountain firs and pines stood in the cold too
            if (m != 1f)
            {
                zdo.Set("Plant.m_growTime", plant.m_growTime * m);
                zdo.Set("Plant.m_growTimeMax", plant.m_growTimeMax * m);
                any = true;
            }
            if (any) { zdo.Set("HasFields", true); zdo.Set("HasFieldsPlant", true); }
            return true;
        }

        // The tree itself, where its stump stood, turned and sized like the stump (which the game gave
        // the felled tree's size)
        private static bool Grow(int tree, ZDO stump)
        {
            var zdo = Create(tree, stump.GetPosition(), stump.GetRotation());
            if (zdo == null) return false;
            var scale = stump.GetVec3(ZDOVars.s_scaleHash, Vector3.zero);
            if (scale != Vector3.zero) zdo.Set(ZDOVars.s_scaleHash, scale);
            else if (stump.GetFloat(ZDOVars.s_scaleScalarHash, out float s)) zdo.Set(ZDOVars.s_scaleScalarHash, s);
            return true;
        }

        internal static ZDO Create(int hash, Vector3 pos, Quaternion rot)
        {
            var prefab = ZNetScene.instance.GetPrefab(hash);
            var view = prefab != null ? prefab.GetComponent<ZNetView>() : null;
            if (view == null) return null;
            var zdo = ZDOMan.instance.CreateNewZDO(pos, hash);
            zdo.Persistent = view.m_persistent;
            zdo.Type = view.m_type;
            zdo.Distant = view.m_distant;
            zdo.SetPrefab(hash);
            zdo.SetRotation(rot);
            return zdo;
        }
    }
}
