using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace LumaPlayerLimit
{
    [BepInProcess("Luma Island.exe")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "lumaisland.playerlimit";
        public const string PluginName = "Luma Player Limit";
        public const string PluginVersion = "0.1.1";

        internal static Plugin Instance;
        internal static ConfigEntry<int> MaxPlayers;
        internal static BepInEx.Logging.ManualLogSource Log => Instance?.Logger;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;

            MaxPlayers = Config.Bind(
                "General",
                "MaxPlayers",
                8,
                new ConfigDescription(
                    "Desired multiplayer cap. The stock game is built for 4 players, so keep this reasonable.",
                    new AcceptableValueRange<int>(4, 32)));

            _harmony = new Harmony(PluginGuid);

            Patches.JoinGameRow.Patch(_harmony);
            Patches.LobbyUtility.Patch(_harmony);
            Patches.SteamLobbyController.Patch(_harmony);

            Logger.LogInfo(string.Format("{0} loaded. MaxPlayers={1}", PluginName, GetConfiguredMaxPlayers()));
        }

        internal static int GetConfiguredMaxPlayers()
        {
            var configured = MaxPlayers != null ? MaxPlayers.Value : 8;
            return Math.Max(4, configured);
        }
    }
}
