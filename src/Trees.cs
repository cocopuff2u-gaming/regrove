using System.Collections.Generic;
using UnityEngine;

namespace Regrove
{
    // What the game's prefabs say about trees: which stump each tree leaves, which sapling grows into
    // it, and the wards and build pieces that keep saplings away. Read once, when the scene is up.
    internal static class Trees
    {
        // stump prefab -> the trees that leave it
        public static readonly Dictionary<int, List<int>> Stumps = new Dictionary<int, List<int>>();
        public static readonly Dictionary<int, int> StumpOf = new Dictionary<int, int>();      // tree -> stump
        public static readonly Dictionary<int, int> SaplingOf = new Dictionary<int, int>();    // tree -> sapling
        public static readonly HashSet<int> Saplings = new HashSet<int>();
        public static readonly Dictionary<int, float> WardRadius = new Dictionary<int, float>();
        public static readonly HashSet<int> Pieces = new HashSet<int>();
        public static bool Ready { get; private set; }

        public static void Load()
        {
            var scene = ZNetScene.instance;
            if (Ready || scene == null || scene.m_prefabs == null || scene.m_prefabs.Count == 0) return;
            var grownFrom = new Dictionary<int, int>();    // tree -> the sapling that grows into it
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
                    int stump = tree.m_stubPrefab.name.GetStableHashCode();
                    StumpOf[hash] = stump;
                    if (!Stumps.TryGetValue(stump, out var trees)) Stumps[stump] = trees = new List<int>();
                    trees.Add(hash);
                    continue;
                }
                var plant = go.GetComponentInChildren<Plant>(true);
                if (plant?.m_grownPrefabs == null) continue;
                foreach (var grown in plant.m_grownPrefabs)
                {
                    if (grown == null) continue;
                    int g = grown.name.GetStableHashCode();
                    if (!grownFrom.ContainsKey(g)) grownFrom[g] = hash;
                }
            }
            foreach (int tree in StumpOf.Keys)
            {
                // the snowy mountain firs and pines and the autumn birches have no sapling of their own:
                // they come back as the plain tree of their kind
                if (grownFrom.TryGetValue(tree, out int sapling) || Family(Name(tree), grownFrom, out sapling))
                {
                    SaplingOf[tree] = sapling;
                    Saplings.Add(sapling);
                }
            }
            foreach (var kv in Stumps)
            {
                var names = new List<string>();
                foreach (int t in kv.Value)
                    names.Add(SaplingOf.TryGetValue(t, out int s)
                        ? Name(t) + " -> " + Name(s) + " (" + Describe(scene.GetPrefab(s).GetComponentInChildren<Plant>(true)) + ")"
                        : Name(t) + " (no sapling)");
                ZLog.Log($"Regrove: {Name(kv.Key)}: {string.Join(", ", names)}");
            }
            Ready = true;
        }

        private static readonly (string kind, string tree)[] families = { ("birch", "Birch1"), ("fir", "FirTree"), ("pine", "Pinetree_01") };

        private static bool Family(string tree, Dictionary<int, int> grownFrom, out int sapling)
        {
            sapling = 0;
            string t = tree.ToLowerInvariant();
            foreach (var (kind, plain) in families)
                if (t.Contains(kind)) return grownFrom.TryGetValue(plain.GetStableHashCode(), out sapling);
            return false;
        }

        // what the game itself says about a sapling: where it grows, how long it takes, how much room it needs
        private static string Describe(Plant p) =>
            $"{p.m_biome}; grows in {p.m_growTime / 60f:F0}-{p.m_growTimeMax / 60f:F0} min, needs {p.m_growRadius:0.#} m of room";

        public static string Name(int prefab)
        {
            var go = ZNetScene.instance?.GetPrefab(prefab);
            return go != null ? go.name : prefab.ToString();
        }
    }
}
