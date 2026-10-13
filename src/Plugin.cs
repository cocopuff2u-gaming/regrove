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
        public const string VERSION = "1.0.0";
        // does the same job; both at once would leave two saplings per stump
        private const string TREESURRECTION = "ArgusMagnus.ServersideQoL.Treesurrection";

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> SaplingChance;
        internal static ConfigEntry<float> SaplingDelay;
        internal static ConfigEntry<float> GrowTime;
        internal static ConfigEntry<float> RegrowDays;
        internal static ConfigEntry<bool> ProtectWards;
        internal static ConfigEntry<float> ProtectRadius;
        internal static bool blocked;

        private void Awake()
        {
            Enabled = Config.Bind("General", "Enabled", true, "Turns the whole mod on or off");
            SaplingChance = Config.Bind("Stumps", "SaplingChance", 100,
                new ConfigDescription("Chance in percent that a stump a player removes leaves a sapling of its tree", new AcceptableValueRange<int>(0, 100)));
            SaplingDelay = Config.Bind("Stumps", "SaplingDelaySeconds", 5f,
                "Seconds between the stump going and the sapling appearing, so the last axe swing doesn't hit it");
            GrowTime = Config.Bind("Stumps", "GrowTimeMultiplier", 1f,
                "How long a regrowing sapling takes compared with one a player plants (0.5 = twice as fast)");
            RegrowDays = Config.Bind("Regrow", "StumpRegrowDays", 3f,
                "In-game days a stump can stand before it turns into a sapling by itself, without anyone removing it. 0 turns this off");
            ProtectWards = Config.Bind("Protect", "InsideWards", true, "No saplings inside an active ward");
            ProtectRadius = Config.Bind("Protect", "BuildRadius", 20f,
                new ConfigDescription("No saplings within this many metres of anything a player built. 0 turns this off", new AcceptableValueRange<float>(0f, 60f)));
            new Harmony(GUID).PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo($"{NAME} {VERSION} loaded");
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

        private void Update() => Stumps.Update();
    }
}
