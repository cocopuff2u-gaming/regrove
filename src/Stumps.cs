using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Regrove
{
    // Stumps turn back into saplings of their tree: when a player removes one, or, left standing,
    // after StumpRegrowDays. All on the server, with plain game objects, so players need no mod.
    internal static class Stumps
    {
        private static readonly int SeenHash = "regrove_seen".GetStableHashCode();   // when the sweep first saw a stump
        private static readonly int OursHash = "HasFieldsPlant".GetStableHashCode();  // a sapling Regrove made
        private const int SWEEP_BUDGET = 4000;          // objects looked at per frame
        private const float SWEEP_EVERY = 300f;         // seconds between sweeps
        private const int MAX_PER_SWEEP = 50;           // stumps turned into saplings per sweep

        private struct Pending { public float at; public int stump; public Vector3 pos; public Quaternion rot; }
        private static readonly List<Pending> pending = new List<Pending>();
        private static readonly HashSet<ZDOID> ours = new HashSet<ZDOID>();   // stumps Regrove removes itself
        private static readonly List<ZDO> near = new List<ZDO>();
        private static readonly List<ZDOID> due = new List<ZDOID>();
        private static int sector = -1, seen, stamped, busy, growing, sweeps;
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
                if (zdo == null || !Trees.Stumps.ContainsKey(zdo.GetPrefab())) return;
                if (Random.Range(0, 100) >= Plugin.SaplingChance.Value) return;
                pending.Add(new Pending { at = Time.time + Plugin.SaplingDelay.Value, stump = zdo.GetPrefab(), pos = zdo.GetPosition(), rot = zdo.GetRotation() });
            }
        }

        public static void Update()
        {
            if (!Active) return;
            Trees.Load();
            if (!Trees.Ready) return;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                if (Time.time < p.at) continue;
                pending.RemoveAt(i);
                if (!Protected(p.pos)) Sow(p.stump, p.pos, p.rot);
            }
            Sweep();
        }

        // Left-standing stumps, a slice of the world each frame. Each gets a first-seen time; once it
        // is old enough and no player is near it, it becomes a sapling.
        private static void Sweep()
        {
            if (Plugin.RegrowDays.Value <= 0f) return;
            var bySector = ZDOMan.instance.m_objectsBySector;
            if (bySector == null) return;
            if (sector < 0)
            {
                if (Time.time < nextSweep) return;
                sector = 0; seen = stamped = busy = growing = 0; due.Clear();
            }
            long now = ZNet.instance.GetTime().Ticks;
            long day = (EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800L) * System.TimeSpan.TicksPerSecond;
            long age = (long)(Plugin.RegrowDays.Value * day);
            int budget = SWEEP_BUDGET;
            while (sector < bySector.Length && budget > 0)
            {
                var list = bySector[sector++];
                if (list == null) continue;
                budget -= list.Count;
                foreach (var zdo in list)
                {
                    int prefab = zdo.GetPrefab();
                    if (Trees.Saplings.Contains(prefab) && zdo.GetBool(OursHash)) growing++;
                    if (!Trees.Stumps.ContainsKey(prefab)) continue;
                    seen++;
                    if (Busy(zdo)) { busy++; continue; }
                    long first = zdo.GetLong(SeenHash);
                    if (first == 0L) { zdo.Set(SeenHash, now); stamped++; }
                    else if (now - first >= age && due.Count < MAX_PER_SWEEP) due.Add(zdo.m_uid);
                }
            }
            if (sector < bySector.Length) return;
            sector = -1;
            nextSweep = Time.time + SWEEP_EVERY;
            int grown = 0, kept = 0;
            foreach (var id in due)
            {
                var zdo = ZDOMan.instance.GetZDO(id);
                if (zdo == null || Busy(zdo)) continue;
                var pos = zdo.GetPosition();
                // kept, or no sapling can grow there: ask again in another StumpRegrowDays
                if (Protected(pos) || !Sow(zdo.GetPrefab(), pos, zdo.GetRotation())) { zdo.Set(SeenHash, now); kept++; continue; }
                ours.Add(zdo.m_uid);
                zdo.SetOwner(ZDOMan.GetSessionID());
                ZDOMan.instance.DestroyZDO(zdo);
                grown++;
            }
            if (due.Count > 0 || sweeps++ == 0) ZLog.Log($"Regrove: {seen} stumps standing ({stamped} new, {busy} near players), {grown} turned into saplings, {kept} kept (near a base, or no sapling grows there); {growing} saplings regrowing");
            due.Clear();
        }

        // a player has it loaded: leave it to the game there
        private static bool Busy(ZDO zdo)
        {
            if (!zdo.HasOwner()) return false;
            long owner = zdo.GetOwner();
            return owner != ZDOMan.GetSessionID() && ZNet.instance.GetPeer(owner) != null;
        }

        private static void FindNear(Vector3 pos)
        {
            near.Clear();
            // the zone around pos and the 8 next to it: 64 m or more in every direction
            ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(pos), new SimulationDistance(1, 0, classic: true), near);
        }

        private static bool Protected(Vector3 pos)
        {
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

        // Which sapling: the stump's own tree when only one tree leaves that stump, otherwise
        // the most common of those trees around it
        private static int SaplingFor(int stump, Vector3 pos)
        {
            var trees = Trees.Stumps[stump];
            int best = 0, bestCount = -1;
            if (trees.Count > 1 && !OneSapling(trees))
            {
                FindNear(pos);
                var counts = new Dictionary<int, int>();
                foreach (var zdo in near)
                {
                    int prefab = zdo.GetPrefab();
                    if (!trees.ContainsKey(prefab)) continue;
                    counts.TryGetValue(prefab, out int c);
                    counts[prefab] = c + 1;
                }
                foreach (var kv in counts)
                    if (kv.Value > bestCount) { best = trees[kv.Key]; bestCount = kv.Value; }
            }
            if (best == 0) foreach (var kv in trees) { best = kv.Value; break; }
            return best;
        }

        private static bool OneSapling(Dictionary<int, int> trees)
        {
            int first = 0;
            foreach (var kv in trees)
            {
                if (first == 0) first = kv.Value;
                else if (kv.Value != first) return false;
            }
            return true;
        }

        // A new sapling, made the way the game makes one, with a few of its settings changed for this
        // one object (the game's own per-object overrides, which every client applies when it loads it)
        private static bool Sow(int stump, Vector3 pos, Quaternion rot)
        {
            int hash = SaplingFor(stump, pos);
            var prefab = ZNetScene.instance.GetPrefab(hash);
            var view = prefab != null ? prefab.GetComponent<ZNetView>() : null;
            var plant = prefab != null ? prefab.GetComponentInChildren<Plant>(true) : null;
            if (view == null || plant == null) return false;
            // a sapling outside its biomes never grows: leave the ground bare there
            if (WorldGenerator.instance != null && (WorldGenerator.instance.GetBiome(pos) & plant.m_biome) == 0) return false;
            var zdo = ZDOMan.instance.CreateNewZDO(pos, hash);
            zdo.Persistent = view.m_persistent;
            zdo.Type = view.m_type;
            zdo.Distant = view.m_distant;
            zdo.SetPrefab(hash);
            zdo.SetRotation(rot);
            zdo.Set(ZDOVars.s_plantTime, ZNet.instance.GetTime().Ticks);
            float m = Mathf.Max(0f, Plugin.GrowTime.Value);
            zdo.Set("HasFields", true);
            zdo.Set("HasFieldsPlant", true);
            zdo.Set("Plant.m_growRadius", 0f);               // grows where the old tree stood, among the others
            zdo.Set("Plant.m_needCultivatedGround", false);
            zdo.Set("Plant.m_destroyIfCantGrow", false);
            zdo.Set("Plant.m_tolerateCold", true);           // the mountain firs and pines stood in the cold too
            zdo.Set("Plant.m_growTime", plant.m_growTime * m);
            zdo.Set("Plant.m_growTimeMax", plant.m_growTimeMax * m);
            return true;
        }
    }
}
