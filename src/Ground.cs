using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace Regrove
{
    // Paths, roads and fields: ground players painted with the hoe or cultivator. Every zone someone
    // worked in has a "_TerrainCompiler" object whose ZDO holds the changes as a gzipped blob: per
    // vertex of the zone's 65x65 grid (1 m apart) a height change and an RGBA paint colour
    // (r = dirt path, g = cultivated, b = paved).
    internal static class Ground
    {
        private const int W = 65, N = W * W;
        private const float PAINTED = 0.25f;
        private static readonly int CompilerHash = "_TerrainCompiler".GetStableHashCode();
        private static readonly Dictionary<ZDOID, (uint rev, bool[] painted)> cache = new Dictionary<ZDOID, (uint, bool[])>();
        private static readonly List<ZDO> near = new List<ZDO>();

        // anything painted within 2 m of pos
        public static bool Worked(Vector3 pos)
        {
            var zone = ZoneSystem.GetZone(pos);
            var center = ZoneSystem.GetZonePos(zone);
            near.Clear();
            ZDOMan.instance.FindSectorObjects(zone, new SimulationDistance(0, 0, classic: true), near);
            foreach (var zdo in near)
            {
                if (zdo.GetPrefab() != CompilerHash || ZoneSystem.GetZone(zdo.GetPosition()) != zone) continue;
                var painted = Painted(zdo);
                if (painted == null) return false;
                int ci = Mathf.RoundToInt(pos.x - (center.x - 32f)), cj = Mathf.RoundToInt(pos.z - (center.z - 32f));
                for (int j = cj - 2; j <= cj + 2; j++)
                    for (int i = ci - 2; i <= ci + 2; i++)
                        if (i >= 0 && j >= 0 && i < W && j < W && painted[j * W + i]) return true;
                return false;
            }
            return false;
        }

        private static bool[] Painted(ZDO zdo)
        {
            uint rev = zdo.DataRevision;
            if (cache.TryGetValue(zdo.m_uid, out var c) && c.rev == rev) return c.painted;
            bool[] painted = null;
            try { painted = Decode(zdo.GetByteArray(ZDOVars.s_TCData)); }
            catch (System.Exception e) { ZLog.LogWarning("Regrove: terrain data not understood: " + e.Message); }
            if (cache.Count > 2000) cache.Clear();
            cache[zdo.m_uid] = (rev, painted);
            return painted;
        }

        private static bool[] Decode(byte[] blob)
        {
            if (blob == null || blob.Length == 0) return null;
            using (var gz = new GZipStream(new MemoryStream(blob), CompressionMode.Decompress))
            using (var br = new BinaryReader(gz))
            {
                br.ReadInt32();                                             // version
                br.ReadInt32();                                             // operations
                br.ReadSingle(); br.ReadSingle(); br.ReadSingle(); br.ReadSingle();   // last op point and radius
                int n = br.ReadInt32();
                if (n != N) throw new InvalidDataException("height grid of " + n);
                for (int i = 0; i < n; i++)
                    if (br.ReadBoolean()) { br.ReadSingle(); br.ReadSingle(); }
                int m = br.ReadInt32();
                if (m != N) throw new InvalidDataException("paint grid of " + m);
                var painted = new bool[N];
                bool any = false;
                for (int i = 0; i < m; i++)
                {
                    if (!br.ReadBoolean()) continue;
                    float r = br.ReadSingle(), g = br.ReadSingle(), b = br.ReadSingle();
                    br.ReadSingle();
                    if (r > PAINTED || g > PAINTED || b > PAINTED) painted[i] = any = true;
                }
                return any ? painted : null;
            }
        }
    }
}
