using System.Collections.Generic;
using UnityEngine;

namespace Regrove
{
    // CountdownSigns: a small sign beside each stump and regrowing sapling near a player, saying how
    // long it has left. Plain game signs, so players need no mod. The server places them, never saves
    // them (they go with a restart), keeps their text up to date and takes them away again.
    internal static class Signs
    {
        private const int MAX = 64;          // signs at once
        private static readonly int SignHash = "sign".GetStableHashCode();
        private struct Shown { public ZDOID sign; public string text; }
        private static readonly Dictionary<ZDOID, Shown> shown = new Dictionary<ZDOID, Shown>();
        private static readonly Dictionary<ZDOID, (ZDO target, Vector3 viewer)> wanted = new Dictionary<ZDOID, (ZDO, Vector3)>();
        private static readonly Dictionary<ZDOID, (bool kept, float until)> keptCache = new Dictionary<ZDOID, (bool, float)>();
        private static readonly List<ZDO> near = new List<ZDO>();
        private static readonly List<ZDOID> gone = new List<ZDOID>();
        private static readonly HashSet<ZDOID> live = new HashSet<ZDOID>();
        private static float next;

        public static void Update()
        {
            if (Time.time < next) return;
            next = Time.time + 2f;
            if (!Plugin.CountdownSigns.Value) { if (shown.Count > 0) RemoveAll(); return; }
            wanted.Clear();
            float r = Plugin.SignRadius.Value;
            foreach (var peer in ZNet.instance.GetPeers())
            {
                Vector3 viewer = peer.m_refPos;
                near.Clear();
                ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(viewer), new SimulationDistance(1, 0, classic: true), near);
                foreach (var zdo in near)
                {
                    if (wanted.Count >= MAX) break;
                    if (wanted.ContainsKey(zdo.m_uid) || Utils.DistanceXZ(zdo.GetPosition(), viewer) > r) continue;
                    int prefab = zdo.GetPrefab();
                    if (Trees.Stumps.ContainsKey(prefab) || (Trees.Saplings.Contains(prefab) && Stumps.IsOurs(zdo)))
                        wanted[zdo.m_uid] = (zdo, viewer);
                }
            }
            live.Clear();
            foreach (var kv in wanted)
            {
                string text = Text(kv.Value.target);
                if (text == null) continue;
                live.Add(kv.Key);
                if (shown.TryGetValue(kv.Key, out var s))
                {
                    var sign = ZDOMan.instance.GetZDO(s.sign);
                    if (sign != null)
                    {
                        if (s.text != text) { sign.Set(ZDOVars.s_text, text); shown[kv.Key] = new Shown { sign = s.sign, text = text }; }
                        continue;
                    }
                }
                var made = Place(kv.Value.target, kv.Value.viewer, text);
                if (made != null) shown[kv.Key] = new Shown { sign = made.m_uid, text = text };
            }
            gone.Clear();
            foreach (var kv in shown)
                if (!live.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var id in gone) Remove(id);
        }

        private static string Text(ZDO target)
        {
            long now = ZNet.instance.GetTime().Ticks;
            int prefab = target.GetPrefab();
            if (Trees.Stumps.ContainsKey(prefab))
            {
                if (Plugin.RegrowDays.Value <= 0f) return null;
                if (Trees.Excluded(Stumps.TreeOf(target))) return null;
                if (Kept(target)) return "Stays\n(near a base)";
                long first = target.GetLong(Stumps.SeenHash);
                long due = (first != 0L ? first : now) + Stumps.RegrowAge();
                string what = Plugin.RegrowAs.Value == Plugin.Regrowth.Tree ? "Tree" : "Sapling";
                return due <= now ? what + "\nany moment now" : what + " in\n" + Span(due - now);
            }
            // a regrowing sapling: the game's own grow time for it, from its seed
            long planted = target.GetLong(ZDOVars.s_plantTime);
            if (planted == 0L) return null;
            float min = target.GetFloat("Plant.m_growTime"), max = target.GetFloat("Plant.m_growTimeMax");
            var state = Random.state;
            Random.InitState(target.GetInt(ZDOVars.s_seed));
            float grow = Mathf.Lerp(min, max, Random.value);
            Random.state = state;
            long ready = planted + (long)(grow * System.TimeSpan.TicksPerSecond);
            return ready <= now ? "Tree\nany moment now" : "Tree in\n" + Span(ready - now);
        }

        // near a base: checked once a minute per stump, not every update
        private static bool Kept(ZDO stump)
        {
            if (keptCache.TryGetValue(stump.m_uid, out var k) && Time.time < k.until) return k.kept;
            bool kept = Stumps.Protected(stump.GetPosition());
            if (keptCache.Count > 4000) keptCache.Clear();
            keptCache[stump.m_uid] = (kept, Time.time + 60f);
            return kept;
        }

        // played time, as the game's clock only runs while someone is online
        private static string Span(long ticks)
        {
            var t = System.TimeSpan.FromTicks(ticks);
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes:D2}m";
            if (t.TotalMinutes >= 1) return $"{t.Minutes}m";
            return "under a minute";
        }

        // a free-standing sign between the target and the player who brought it near, facing them
        private static ZDO Place(ZDO target, Vector3 viewer, string text)
        {
            var prefab = ZNetScene.instance.GetPrefab(SignHash);
            var view = prefab != null ? prefab.GetComponent<ZNetView>() : null;
            if (view == null) return null;
            var pos = target.GetPosition();
            var toViewer = viewer - pos; toViewer.y = 0f;
            if (toViewer.sqrMagnitude < 0.01f) toViewer = Vector3.forward;
            toViewer.Normalize();
            bool stump = Trees.Stumps.ContainsKey(target.GetPrefab());
            var at = pos + toViewer * (stump ? 1.3f : 0.7f) + Vector3.up * 0.9f;
            var zdo = ZDOMan.instance.CreateNewZDO(at, SignHash);
            zdo.Persistent = false;          // never saved
            zdo.Type = view.m_type;
            zdo.Distant = view.m_distant;
            zdo.SetPrefab(SignHash);
            zdo.SetRotation(Quaternion.LookRotation(toViewer));
            zdo.Set("HasFields", true);
            zdo.Set("HasFieldsWearNTear", true);
            zdo.Set("WearNTear.m_supports", false);      // stands in the air
            zdo.Set("HasFieldsPiece", true);
            zdo.Set("Piece.m_canBeRemoved", false);
            zdo.Set(ZDOVars.s_text, text);
            return zdo;
        }

        private static void Remove(ZDOID target)
        {
            if (shown.TryGetValue(target, out var s))
            {
                var sign = ZDOMan.instance.GetZDO(s.sign);
                if (sign != null) Stumps.Remove(sign);
            }
            shown.Remove(target);
        }

        public static void RemoveAll()
        {
            gone.Clear();
            gone.AddRange(shown.Keys);
            foreach (var id in gone) Remove(id);
        }
    }
}
