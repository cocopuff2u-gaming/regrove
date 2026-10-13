using System.Collections.Generic;
using UnityEngine;

namespace Regrove
{
    // Wood: the drops of stumps Regrove removes itself (RemoveOnFell), and with SpecialWoodOnly the
    // plain wood a log or stump drops, turned into its tree's special wood, one for one. The player
    // breaking it makes the drops, so the server swaps each plain wood for the special one as soon
    // as it shows up.
    internal static class Wood
    {
        private const float WATCH = 3f;      // seconds a broken log or stump's drops are looked for
        private struct Watch { public Vector3 pos; public float radius, until; public int special; public HashSet<ZDOID> before; }
        private static readonly List<Watch> watches = new List<Watch>();
        private static readonly List<ZDO> near = new List<ZDO>();
        private static float next;

        // a log or stump was broken: its drops arrive in a moment
        public static void Broken(ZDO zdo)
        {
            if (!Plugin.SpecialWoodOnly.Value || !Trees.SpecialWood.TryGetValue(zdo.GetPrefab(), out int special)) return;
            var pos = zdo.GetPosition();
            // logs scatter their drops up to 2 m along their length, stumps right where they stood
            float radius = Trees.Stumps.ContainsKey(zdo.GetPrefab()) ? 2.5f : 4f;
            var before = new HashSet<ZDOID>();
            Find(pos);
            foreach (var z in near) if (z.GetPrefab() == Trees.Wood) before.Add(z.m_uid);
            watches.Add(new Watch { pos = pos, radius = radius, until = Time.time + WATCH, special = special, before = before });
        }

        public static void Update()
        {
            if (watches.Count == 0 || Time.time < next) return;
            next = Time.time + 0.1f;
            for (int i = watches.Count - 1; i >= 0; i--)
            {
                var w = watches[i];
                Find(w.pos);
                foreach (var zdo in near)
                {
                    if (zdo.GetPrefab() != Trees.Wood || w.before.Contains(zdo.m_uid) || Utils.DistanceXZ(zdo.GetPosition(), w.pos) > w.radius) continue;
                    int stack = Stack(zdo);
                    var pos = zdo.GetPosition();
                    Stumps.Remove(zdo);
                    Drop(w.special, stack, pos);
                }
                if (Time.time > w.until) watches.RemoveAt(i);
            }
        }

        // What a stump drops when it's dug out, for one Regrove removes itself
        public static void StumpDrops(int stump, Vector3 pos)
        {
            if (!Trees.StumpDrops.TryGetValue(stump, out var table)) return;
            Trees.SpecialWood.TryGetValue(stump, out int special);
            var counts = new Dictionary<int, int>();
            foreach (var go in table.GetDropList())
            {
                int item = go.name.GetStableHashCode();
                if (item == Trees.Wood && special != 0 && Plugin.SpecialWoodOnly.Value) item = special;
                counts.TryGetValue(item, out int c);
                counts[item] = c + 1;
            }
            foreach (var kv in counts) Drop(kv.Key, kv.Value, pos + Vector3.up * 0.5f);
        }

        private static void Find(Vector3 pos)
        {
            near.Clear();
            ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(pos), new SimulationDistance(1, 0, classic: true), near);
        }

        private static int Stack(ZDO zdo)
        {
            var prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab())?.GetComponent<ItemDrop>();
            if (prefab == null) return 1;
            var data = prefab.m_itemData.Clone();
            data.m_stack = 1;
            ItemDrop.LoadFromZDO(data, zdo);
            return Mathf.Max(1, data.m_stack);
        }

        // an item lying on the ground, made the way the game saves one
        internal static void Drop(int item, int stack, Vector3 pos)
        {
            var prefab = ZNetScene.instance.GetPrefab(item);
            var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            var view = prefab != null ? prefab.GetComponent<ZNetView>() : null;
            if (drop == null || view == null) return;
            int max = Mathf.Max(1, drop.m_itemData.m_shared.m_maxStackSize);
            while (stack > 0)
            {
                var data = drop.m_itemData.Clone();
                data.m_stack = Mathf.Min(stack, max);
                data.m_dropPrefab = prefab;
                stack -= data.m_stack;
                var zdo = ZDOMan.instance.CreateNewZDO(pos, item);
                zdo.Persistent = view.m_persistent;
                zdo.Type = view.m_type;
                zdo.Distant = view.m_distant;
                zdo.SetPrefab(item);
                zdo.SetRotation(Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                ItemDrop.SaveToZDO(data, zdo);
            }
        }
    }
}
