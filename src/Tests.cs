#if SELFTEST
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;

namespace Regrove
{
    // Server-side self-test, only in a build made with -p:SelfTest=true. Started by a file named
    // regrove.selftest in BepInEx/config. On an empty spot of Meadows it does what a player's game would
    // send the server (a stump or log destroyed, wood dropped, a tree felled and its stump appearing, a
    // player standing nearby), checks what Regrove does about it, logs "Regrove TEST PASS/FAIL", then
    // removes everything it made and puts the settings back.
    internal static class Tests
    {
        public static readonly List<Vector3> Viewers = new List<Vector3>();
        private static readonly List<ZDO> near = new List<ZDO>();
        private static readonly HashSet<ZDOID> before = new HashSet<ZDOID>();
        private static readonly List<Vector3> sites = new List<Vector3>();
        private static bool started;
        private static int pass, fail;

        private static string Trigger => Path.Combine(Paths.ConfigPath, "regrove.selftest");

        public static void Update(Plugin host)
        {
            if (started || !Trees.Ready || Time.time < 60f || !File.Exists(Trigger)) return;
            started = true;
            try { File.Delete(Trigger); } catch { }
            host.StartCoroutine(Run(host));
        }

        private static IEnumerator Run(Plugin host)
        {
            ZLog.Log("Regrove TEST starting");
            if (!FindSite(out var site)) { ZLog.Log("Regrove TEST no empty Meadows spot found"); yield break; }
            for (int i = 0; i < 10; i++) sites.Add(site + new Vector3(i * 30f, 0f, 0f));
            for (int i = 0; i < sites.Count; i++) sites[i] = Ground(sites[i]);
            ZLog.Log($"Regrove TEST site {site.x:0},{site.z:0}");
            foreach (var p in sites) { Find(p, 8f, 0); foreach (var z in near) before.Add(z.m_uid); }

            // the settings as they were, then the ones the tests need
            var cfg = host.Config;
            bool save = cfg.SaveOnConfigSet;
            cfg.SaveOnConfigSet = false;
            var was = (Plugin.SaplingChance.Value, Plugin.SaplingDelay.Value, Plugin.RemoveOnFell.Value, Plugin.SpecialWoodOnly.Value,
                Plugin.ExcludeTrees.Value, Plugin.CountdownSigns.Value, Plugin.ProtectWards.Value, Plugin.ProtectRadius.Value, Plugin.RegrowDays.Value);
            Plugin.SaplingChance.Value = 100; Plugin.SaplingDelay.Value = 1f; Plugin.RemoveOnFell.Value = false;
            Plugin.SpecialWoodOnly.Value = false; Plugin.ExcludeTrees.Value = ""; Plugin.CountdownSigns.Value = false;
            Plugin.ProtectWards.Value = true; Plugin.ProtectRadius.Value = 20f; Plugin.RegrowDays.Value = 3f;
            Trees.SettingsChanged();

            int pineStump = "Pinetree_01_Stub".GetStableHashCode(), pineSapling = "PineTree_Sapling".GetStableHashCode();
            int core = "RoundLog".GetStableHashCode(), sign = "sign".GetStableHashCode();

            // 1. a player digs out a stump: a sapling of its tree, with Regrove's settings on it
            var s0 = sites[0];
            PlayerRemoves(Make(pineStump, s0));
            yield return new WaitForSeconds(2.5f);
            var sap = Find(s0, 0.5f, pineSapling);
            Check("dug stump leaves a sapling", sap.Count == 1 && Stumps.IsOurs(sap[0]) && sap[0].GetFloat("Plant.m_growRadius", -1f) == 0f
                && sap[0].GetInt(ZDOVars.s_seed) != 0 && sap[0].GetLong(ZDOVars.s_plantTime) != 0L, $"{sap.Count} sapling(s)");

            // 2. countdown signs beside a sapling and a stump with a player near, gone when switched off
            var s1 = sites[1];
            var stump1 = Make(pineStump, s1);
            stump1.Set(Stumps.SeenHash, ZNet.instance.GetTime().Ticks);
            Plugin.CountdownSigns.Value = true;
            Viewers.Add(s0 + new Vector3(4f, 0f, 0f)); Viewers.Add(s1 + new Vector3(4f, 0f, 0f));
            yield return new WaitForSeconds(4.5f);
            var signs0 = Find(s0, 2.5f, sign); var signs1 = Find(s1, 3f, sign);
            string t0 = signs0.Count > 0 ? signs0[0].GetString(ZDOVars.s_text) : "", t1 = signs1.Count > 0 ? signs1[0].GetString(ZDOVars.s_text) : "";
            Check("sign on a regrowing sapling", signs0.Count == 1 && t0.StartsWith("Tree") && !signs0[0].Persistent, "\"" + t0.Replace("\n", " ") + "\"");
            Check("sign on a stump", signs1.Count == 1 && t1.StartsWith("Sapling in") && !signs1[0].Persistent, "\"" + t1.Replace("\n", " ") + "\"");
            // only saplings: the stump's sign goes, the sapling's stays
            Plugin.SignsOn.Value = Plugin.SignTargets.Saplings;
            yield return new WaitForSeconds(4.5f);
            Check("SignsOn = Saplings", Find(s0, 2.5f, sign).Count == 1 && Find(s1, 3f, sign).Count == 0, "");
            // a stump that won't grow back gets no sign
            Plugin.SignsOn.Value = Plugin.SignTargets.Stumps;
            Plugin.RegrowChance.Value = 0;
            yield return new WaitForSeconds(4.5f);
            Check("RegrowChance 0: stump stays, no sign", !Stumps.WillRegrow(stump1) && Find(s1, 3f, sign).Count == 0 && Find(s0, 2.5f, sign).Count == 0, "");
            Plugin.RegrowChance.Value = 100;
            Plugin.SignsOn.Value = Plugin.SignTargets.Both;
            // a chance of 50 lets about half the stumps through, the same ones every time
            Plugin.RegrowChance.Value = 50;
            int through = 0; bool same = true;
            for (int i = 0; i < 1000; i++)
            {
                var z = new ZDO { m_uid = new ZDOID(77L + i % 7, (uint)(1000 + i)) };
                bool a = Stumps.WillRegrow(z), b = Stumps.WillRegrow(z);
                if (a) through++;
                same &= a == b;
            }
            Check("RegrowChance 50 lets about half through, always the same ones", through > 420 && through < 580 && same, $"{through} of 1000");
            Plugin.RegrowChance.Value = 100;
            Plugin.CountdownSigns.Value = false;
            Viewers.Clear();
            yield return new WaitForSeconds(4.5f);
            Check("signs go when switched off", Find(s0, 2.5f, sign).Count == 0 && Find(s1, 3f, sign).Count == 0, "");

            // 3. special wood: a dug pine stump's wood turns into core wood
            Plugin.SpecialWoodOnly.Value = true;
            var s2 = sites[2];
            PlayerRemoves(Make(pineStump, s2));
            yield return new WaitForSeconds(0.5f);
            Wood.Drop(Trees.Wood, 1, s2 + new Vector3(0.3f, 0.5f, 0f)); Wood.Drop(Trees.Wood, 1, s2 + new Vector3(-0.3f, 0.8f, 0.2f));
            yield return new WaitForSeconds(1.5f);
            Check("stump wood becomes core wood", Find(s2, 2.5f, Trees.Wood).Count == 0 && Stacks(Find(s2, 2.5f, core)) == 2,
                $"{Find(s2, 2.5f, Trees.Wood).Count} wood, {Stacks(Find(s2, 2.5f, core))} core wood");

            // 4. special wood from a log; a beech log keeps its plain wood
            var s3 = sites[3];
            PlayerRemoves(Make("PineTree_log_half".GetStableHashCode(), s3));
            yield return new WaitForSeconds(0.5f);
            for (int i = 0; i < 3; i++) Wood.Drop(Trees.Wood, 1, s3 + new Vector3(-1.5f + i * 1.5f, 0.5f + 0.3f * i, 0f));
            // the beech's own log, as the game names it
            var beechLogGo = ZNetScene.instance.GetPrefab("Beech1")?.GetComponentInChildren<TreeBase>(true)?.m_logPrefab;
            int beechLog = beechLogGo != null ? beechLogGo.name.GetStableHashCode() : 0;
            bool haveBeech = beechLog != 0;
            Check("found the beech log", haveBeech, beechLogGo != null ? beechLogGo.name : "none");
            var s3b = s3 + new Vector3(0f, 0f, 12f);
            if (haveBeech)
            {
                PlayerRemoves(Make(beechLog, s3b));
                yield return new WaitForSeconds(0.5f);
                Wood.Drop(Trees.Wood, 1, s3b + Vector3.up * 0.5f);
            }
            yield return new WaitForSeconds(1.5f);
            Check("pine log wood becomes core wood", Find(s3, 4f, Trees.Wood).Count == 0 && Stacks(Find(s3, 4f, core)) == 3,
                $"{Find(s3, 4f, Trees.Wood).Count} wood, {Stacks(Find(s3, 4f, core))} core wood");
            if (haveBeech) Check("beech log keeps plain wood", Find(s3b, 3f, Trees.Wood).Count == 1, $"{Find(s3b, 3f, Trees.Wood).Count} wood");
            Plugin.SpecialWoodOnly.Value = false;

            // 5. an excluded tree is left alone
            Plugin.ExcludeTrees.Value = "Pinetree";
            Trees.SettingsChanged();
            var s4 = sites[4];
            PlayerRemoves(Make(pineStump, s4));
            yield return new WaitForSeconds(2.5f);
            Check("excluded tree gets no sapling", Find(s4, 0.5f, pineSapling).Count == 0, "");
            Plugin.ExcludeTrees.Value = "";
            Trees.SettingsChanged();

            // 6. RemoveOnFell: the tree falls, its stump appears and goes again, a sapling and the stump's wood (as core wood)
            Plugin.RemoveOnFell.Value = true; Plugin.SpecialWoodOnly.Value = true;
            var s5 = sites[5];
            PlayerRemoves(Make("Pinetree_01".GetStableHashCode(), s5));
            yield return new WaitForSeconds(0.6f);
            var stump5 = Make(pineStump, s5);
            var id5 = stump5.m_uid;
            yield return new WaitForSeconds(3f);
            Check("RemoveOnFell takes the stump", ZDOMan.instance.GetZDO(id5) == null, "");
            Check("RemoveOnFell plants one sapling", Find(s5, 0.5f, pineSapling).Count == 1, $"{Find(s5, 0.5f, pineSapling).Count} sapling(s)");
            Check("RemoveOnFell drops the stump's wood as core wood", Stacks(Find(s5, 2f, core)) > 0 && Find(s5, 2f, Trees.Wood).Count == 0,
                $"{Stacks(Find(s5, 2f, core))} core wood, {Find(s5, 2f, Trees.Wood).Count} wood");
            Plugin.RemoveOnFell.Value = false; Plugin.SpecialWoodOnly.Value = false;

            // 7. a ward keeps its ground clear
            var s6 = sites[6];
            var ward = Make("guard_stone".GetStableHashCode(), s6);
            ward.Set(ZDOVars.s_enabled, true);
            var s6b = s6 + new Vector3(6f, 0f, 0f);
            PlayerRemoves(Make(pineStump, s6b));
            // 8. and so does a building
            var s7 = sites[7];
            var bench = Make("piece_workbench".GetStableHashCode(), s7);
            bench.Set(ZDOVars.s_creator, 123456789L);
            var s7b = s7 + new Vector3(12f, 0f, 0f);
            PlayerRemoves(Make(pineStump, s7b));
            yield return new WaitForSeconds(2.5f);
            Check("no sapling inside a ward", Find(s6b, 0.5f, pineSapling).Count == 0, "");
            Check("no sapling near a building", Find(s7b, 0.5f, pineSapling).Count == 0, "");

            // 9. the settings file: saved, reloaded
            int reloads = Plugin.Reloads;
            try { File.SetLastWriteTimeUtc(cfg.ConfigFilePath, System.DateTime.UtcNow); File.AppendAllText(cfg.ConfigFilePath, ""); } catch { }
            yield return new WaitForSeconds(3f);
            Check("settings file reloads", Plugin.Reloads > reloads, "");

            // put everything back
            (Plugin.SaplingChance.Value, Plugin.SaplingDelay.Value, Plugin.RemoveOnFell.Value, Plugin.SpecialWoodOnly.Value,
                Plugin.ExcludeTrees.Value, Plugin.CountdownSigns.Value, Plugin.ProtectWards.Value, Plugin.ProtectRadius.Value, Plugin.RegrowDays.Value) = was;
            cfg.SaveOnConfigSet = save;
            Trees.SettingsChanged();
            yield return new WaitForSeconds(4f);
            int removed = 0;
            foreach (var p in sites)
                foreach (var z in Find(p, 15f, 0))
                    if (!before.Contains(z.m_uid)) { Stumps.Remove(z); removed++; }
            ZLog.Log($"Regrove TEST done: {pass} passed, {fail} failed; {removed} test objects removed");
        }

        private static void Check(string name, bool ok, string detail)
        {
            if (ok) pass++; else fail++;
            ZLog.Log($"Regrove TEST {(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? ": " + detail : "")}");
        }

        // what a player's game sends when it destroys something: Regrove didn't remove it itself
        private static void PlayerRemoves(ZDO zdo) => ZDOMan.instance.DestroyZDO(zdo);

        private static ZDO Make(int prefab, Vector3 pos) => Stumps.Create(prefab, pos, Quaternion.identity);

        private static List<ZDO> Find(Vector3 pos, float r, int prefab)
        {
            near.Clear();
            ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(pos), new SimulationDistance(1, 0, classic: true), near);
            var found = new List<ZDO>();
            foreach (var z in near)
                if ((prefab == 0 || z.GetPrefab() == prefab) && Utils.DistanceXZ(z.GetPosition(), pos) <= r) found.Add(z);
            return found;
        }

        private static int Stacks(List<ZDO> items)
        {
            int n = 0;
            foreach (var z in items)
            {
                var drop = ZNetScene.instance.GetPrefab(z.GetPrefab())?.GetComponent<ItemDrop>();
                if (drop == null) continue;
                var data = drop.m_itemData.Clone();
                ItemDrop.LoadFromZDO(data, z);
                n += data.m_stack;
            }
            return n;
        }

        private static Vector3 Ground(Vector3 p) => new Vector3(p.x, WorldGenerator.instance.GetHeight(p.x, p.z), p.z);

        // a stretch of dry Meadows, 300 m long, with nobody's things on it
        private static bool FindSite(out Vector3 site)
        {
            for (int k = 0; k < 3000; k++)
            {
                float a = k * 2.39996f, d = 800f + k * 2f;
                site = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                bool ok = true;
                for (int i = 0; i < 10 && ok; i++)
                {
                    var p = site + new Vector3(i * 30f, 0f, 0f);
                    ok = WorldGenerator.instance.GetBiome(p) == Heightmap.Biome.Meadows
                        && WorldGenerator.instance.GetHeight(p.x, p.z) > ZoneSystem.instance.m_waterLevel + 2f
                        && !Stumps.Protected(p);
                }
                if (ok) return true;
            }
            site = Vector3.zero;
            return false;
        }
    }
}
#endif
