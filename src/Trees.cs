using System.Collections.Generic;
using UnityEngine;

namespace Regrove
{
    // What the game's prefabs say about trees: which stump each tree leaves, which sapling grows into
    // it, and the wards and build pieces that keep saplings away. Read once, when the scene is up.
    internal static class Trees
    {
        // stump prefab -> (tree prefab -> its sapling prefab)
        public static readonly Dictionary<int, Dictionary<int, int>> Stumps = new Dictionary<int, Dictionary<int, int>>();
        public static readonly Dictionary<int, float> WardRadius = new Dictionary<int, float>();
        public static readonly HashSet<int> Pieces = new HashSet<int>();
        public static readonly HashSet<int> Saplings = new HashSet<int>();
        public static bool Ready { get; private set; }

        public static void Load()
        {
            var scene = ZNetScene.instance;
            if (Ready || scene == null || scene.m_prefabs == null || scene.m_prefabs.Count == 0) return;
            var stumpOf = new Dictionary<int, int>();      // tree -> stump
            var saplingOf = new Dictionary<int, int>();    // tree -> sapling
            foreach (var go in scene.m_prefabs)
            {
                if (go == null) continue;
                int hash = go.name.GetStableHashCode();
                var ward = go.GetComponent<PrivateArea>();
                if (ward != null) WardRadius[hash] = ward.m_radius;
                if (go.GetComponent<Piece>() != null) Pieces.Add(hash);
                var tree = go.GetComponentInChildren<TreeBase>(true);
                if (tree != null && tree.m_stubPrefab != null)
                {
                    stumpOf[hash] = tree.m_stubPrefab.name.GetStableHashCode();
                    continue;
                }
                var plant = go.GetComponentInChildren<Plant>(true);
                if (plant?.m_grownPrefabs == null) continue;
                foreach (var grown in plant.m_grownPrefabs)
                {
                    if (grown == null) continue;
                    int g = grown.name.GetStableHashCode();
                    if (!saplingOf.ContainsKey(g)) saplingOf[g] = hash;
                }
            }
            foreach (var kv in stumpOf)
            {
                // the snowy mountain firs and pines and the autumn birches have no sapling of their own:
                // they come back as the plain tree of their kind
                if (!saplingOf.TryGetValue(kv.Key, out int sapling) && !Family(Name(kv.Key), saplingOf, out sapling))
                {
                    ZLog.Log($"Regrove: {Name(kv.Key)} has no sapling, its stumps stay");
                    continue;
                }
                if (!Stumps.TryGetValue(kv.Value, out var trees)) Stumps[kv.Value] = trees = new Dictionary<int, int>();
                trees[kv.Key] = sapling;
                Saplings.Add(sapling);
            }
            foreach (var kv in Stumps)
            {
                var names = new List<string>();
                foreach (var t in kv.Value) names.Add(Name(t.Key) + " -> " + Name(t.Value) + " (" + scene.GetPrefab(t.Value).GetComponentInChildren<Plant>(true).m_biome + ")");
                ZLog.Log($"Regrove: {Name(kv.Key)}: {string.Join(", ", names)}");
            }
            Ready = true;
        }

        private static readonly (string kind, string tree)[] families = { ("birch", "Birch1"), ("fir", "FirTree"), ("pine", "Pinetree_01") };

        private static bool Family(string tree, Dictionary<int, int> saplingOf, out int sapling)
        {
            sapling = 0;
            string t = tree.ToLowerInvariant();
            foreach (var (kind, plain) in families)
                if (t.Contains(kind)) return saplingOf.TryGetValue(plain.GetStableHashCode(), out sapling);
            return false;
        }

        public static string Name(int prefab)
        {
            var go = ZNetScene.instance?.GetPrefab(prefab);
            return go != null ? go.name : prefab.ToString();
        }
    }
}
