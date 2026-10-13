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
        // logs and stumps -> the special wood of their tree (core wood, fine wood, ...), for SpecialWoodOnly
        public static readonly Dictionary<int, int> SpecialWood = new Dictionary<int, int>();
        public static readonly Dictionary<int, DropTable> StumpDrops = new Dictionary<int, DropTable>();
        public static readonly int Wood = "Wood".GetStableHashCode();
        private static readonly Dictionary<int, bool> excluded = new Dictionary<int, bool>();
        public static bool Ready { get; private set; }

        public static void Load()
        {
            var scene = ZNetScene.instance;
            if (Ready || scene == null || scene.m_prefabs == null || scene.m_prefabs.Count == 0) return;
            var grownFrom = new Dictionary<int, int>();    // tree -> the sapling that grows into it
            var logOf = new Dictionary<int, GameObject>();  // tree -> its log
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
                    if (tree.m_logPrefab != null) logOf[hash] = tree.m_logPrefab;
                    var drops = tree.m_stubPrefab.GetComponent<DropOnDestroyed>();
                    if (drops != null) StumpDrops[stump] = drops.m_dropWhenDestroyed;
                    if (!Stumps.TryGetValue(stump, out var trees)) Stumps[stump] = trees = new List<int>();
                    trees.Add(hash);
                    continue;
                }
                var log = go.GetComponent<TreeLog>();
                if (log != null)
                {
                    int special = SpecialIn(log.m_dropWhenDestroyed);
                    if (special != 0) SpecialWood[hash] = special;
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
            // a stump gives the special wood of its tree's logs (the first log, or the halves it breaks into)
            foreach (var kv in logOf)
            {
                int stump = StumpOf[kv.Key];
                if (SpecialWood.ContainsKey(stump)) continue;
                for (var log = kv.Value; log != null; log = log.GetComponent<TreeLog>()?.m_subLogPrefab)
                    if (SpecialWood.TryGetValue(log.name.GetStableHashCode(), out int special)) { SpecialWood[stump] = special; break; }
            }
            var woods = new List<string>();
            foreach (var kv in SpecialWood) woods.Add(Name(kv.Key) + " -> " + Name(kv.Value));
            ZLog.Log($"Regrove: special wood: {string.Join(", ", woods)}");
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

        // the wood in a drop table that isn't plain wood: core wood, fine wood, ancient bark, Yggdrasil
        // wood, blackwood, frost wood (and not the cones and seeds that come with some)
        private static int SpecialIn(DropTable table)
        {
            if (table?.m_drops == null) return 0;
            foreach (var d in table.m_drops)
            {
                if (d.m_item == null) continue;
                string n = d.m_item.name.ToLowerInvariant();
                if (n != "wood" && (n.Contains("wood") || n.Contains("log") || n.Contains("bark"))) return d.m_item.name.GetStableHashCode();
            }
            return 0;
        }

        // ExcludeTrees: names, or the start of names, of trees that are left alone
        public static bool Excluded(int tree)
        {
            if (excluded.TryGetValue(tree, out bool ex)) return ex;
            ex = false;
            string name = Name(tree);
            foreach (string part in Plugin.ExcludeTrees.Value.Split(','))
            {
                string p = part.Trim();
                if (p.Length > 0 && name.StartsWith(p, System.StringComparison.OrdinalIgnoreCase)) { ex = true; break; }
            }
            excluded[tree] = ex;
            return ex;
        }

        public static void SettingsChanged() => excluded.Clear();

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
