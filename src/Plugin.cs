using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;

namespace Regrove
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.cocopuff2u.regrove";
        public const string NAME = "Regrove";
        public const string VERSION = "1.0.1";
        // does the same job; both at once would leave two saplings per stump
        private const string TREESURRECTION = "ArgusMagnus.ServersideQoL.Treesurrection";

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> SaplingChance;
        internal static ConfigEntry<float> SaplingDelay;
        internal static ConfigEntry<float> GrowTime;
        internal static ConfigEntry<bool> RemoveOnFell;
        internal static ConfigEntry<float> RegrowDays;
        internal static ConfigEntry<Regrowth> RegrowAs;
        internal static ConfigEntry<int> RegrowChance;
        internal static ConfigEntry<int> MaxPerRound;
        internal static ConfigEntry<float> RoundMinutes;
        internal static ConfigEntry<SignTargets> SignsOn;
        internal static ConfigEntry<bool> ProtectWards;
        internal static ConfigEntry<float> ProtectRadius;
        internal static ConfigEntry<bool> ProtectGround;
        internal static ConfigEntry<string> ExcludeTrees;
        internal static ConfigEntry<bool> SpecialWoodOnly;
        internal static ConfigEntry<bool> CountdownSigns;
        internal static ConfigEntry<float> SignRadius;
        internal static bool blocked;
        internal static int Reloads;
        private FileSystemWatcher watcher;
        private volatile bool changed;
        private float reloadAt;

        public enum Regrowth { Sapling, Tree }
        public enum SignTargets { Both, Stumps, Saplings }

        private void Awake()
        {
            Enabled = Config.Bind("General", "Enabled", true, "Turns the whole mod on or off");
            SaplingChance = Config.Bind("Stumps", "SaplingChance", 100,
                new ConfigDescription("Chance in percent that a stump a player removes leaves a sapling of its tree", new AcceptableValueRange<int>(0, 100)));
            SaplingDelay = Config.Bind("Stumps", "SaplingDelaySeconds", 5f,
                "Seconds between the stump going and the sapling appearing, so the last axe swing doesn't hit it");
            GrowTime = Config.Bind("Stumps", "GrowTimeMultiplier", 1f,
                "How long a regrowing sapling takes compared with one a player plants (0.5 = twice as fast)");
            RemoveOnFell = Config.Bind("Stumps", "RemoveOnFell", false,
                "Stumps go as soon as their tree falls and the sapling is planted right away, so nobody has to dig them out. The stump's wood is lost");
            RegrowDays = Config.Bind("Regrow", "StumpRegrowDays", 3f,
                "In-game days a stump can stand before it grows back by itself, without anyone removing it. The game's clock only runs while someone is online. 0 turns this off");
            RegrowAs = Config.Bind("Regrow", "RegrowAs", Regrowth.Sapling,
                "What a left stump turns into. Sapling: a sapling of its tree, which then grows. Tree: the tree that stood there, the same kind and size, at once; swamp, Mistlands and Ashlands trees come back too");
            RegrowChance = Config.Bind("Regrow", "RegrowChance", 100,
                new ConfigDescription("Chance in percent that a left stump grows back at all. Each stump's chance is fixed (the same stump always gets the same answer), so below 100 some stumps simply stay, and old clearings fill in patchily", new AcceptableValueRange<int>(0, 100)));
            MaxPerRound = Config.Bind("Regrow", "MaxPerRound", 50,
                new ConfigDescription("Left stumps that may grow back in one round", new AcceptableValueRange<int>(1, 1000)));
            RoundMinutes = Config.Bind("Regrow", "RoundMinutes", 5f,
                new ConfigDescription("Minutes between rounds: how often the world is checked for left stumps that are due", new AcceptableValueRange<float>(1f, 60f)));
            ProtectWards = Config.Bind("Protect", "InsideWards", true, "No saplings inside an active ward");
            ProtectRadius = Config.Bind("Protect", "BuildRadius", 20f,
                new ConfigDescription("No saplings within this many metres of anything a player built. 0 turns this off", new AcceptableValueRange<float>(0f, 60f)));
            ProtectGround = Config.Bind("Protect", "PathsAndFields", false,
                "No saplings on paths, paved roads or cultivated ground, or within 2 m of them");
            ExcludeTrees = Config.Bind("Trees", "ExcludeTrees", "",
                "Trees Regrove leaves alone, by name or the start of a name, comma separated (e.g. \"SwampTree, Oak\"). The server log lists every tree's name at start");
            SpecialWoodOnly = Config.Bind("Wood", "SpecialWoodOnly", false,
                "Logs and stumps drop their tree's special wood in place of plain wood, one for one: core wood from pines, fine wood from birches and oaks, ancient bark from swamp trees, Yggdrasil wood, blackwood, frost wood. Beech and fir have none and still give plain wood");
            CountdownSigns = Config.Bind("Signs", "CountdownSigns", false,
                "A small sign beside each stump and regrowing sapling near a player, saying how long it has left (time played). Plain game signs, so players need no mod; they are never saved");
            SignsOn = Config.Bind("Signs", "SignsOn", SignTargets.Both,
                "Which get a countdown sign: Both, Stumps or Saplings");
            SignRadius = Config.Bind("Signs", "SignRadius", 15f,
                new ConfigDescription("How close, in metres, a stump or sapling must be to a player to get a sign", new AcceptableValueRange<float>(3f, 40f)));
            new Harmony(GUID).PatchAll(Assembly.GetExecutingAssembly());
            WatchConfig();
            Logger.LogInfo($"{NAME} {VERSION} loaded");
        }

        // settings changed in the file take effect a moment later, no restart needed
        private void WatchConfig()
        {
            try
            {
                watcher = new FileSystemWatcher(Path.GetDirectoryName(Config.ConfigFilePath), Path.GetFileName(Config.ConfigFilePath));
                watcher.Changed += (s, e) => changed = true;
                watcher.Created += (s, e) => changed = true;
                watcher.Renamed += (s, e) => changed = true;
                watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size;
                watcher.EnableRaisingEvents = true;
            }
            catch (System.Exception e) { Logger.LogWarning("Settings won't reload by themselves: " + e.Message); }
        }

        private void ReloadIfChanged()
        {
            if (changed) { changed = false; reloadAt = UnityEngine.Time.time + 1f; }   // wait for the editor to finish writing
            if (reloadAt == 0f || UnityEngine.Time.time < reloadAt) return;
            reloadAt = 0f;
            try
            {
                // (without saving the file back, which would only start another reload)
                bool save = Config.SaveOnConfigSet;
                Config.SaveOnConfigSet = false;
                try { Config.Reload(); } finally { Config.SaveOnConfigSet = save; }
                Trees.SettingsChanged();
                Reloads++;
                Logger.LogInfo("Settings reloaded");
            }
            catch (System.Exception e) { Logger.LogWarning("Settings not reloaded: " + e.Message); }
        }

        // every plugin has loaded by now
        private void Start()
        {
            if (Chainloader.PluginInfos.ContainsKey(TREESURRECTION))
            {
                blocked = true;
                Logger.LogError("ServersideQoL.Treesurrection is installed and does the same thing. Regrove is not running: remove one of the two.");
            }
        }

        private void Update()
        {
            ReloadIfChanged();
            Stumps.Update();
#if SELFTEST
            Tests.Update(this);
#endif
        }
    }
}
