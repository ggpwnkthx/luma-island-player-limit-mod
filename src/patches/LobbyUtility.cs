using System;
using System.Reflection;
using HarmonyLib;

namespace LumaPlayerLimit.Patches
{
    internal static class LobbyUtility
    {
        public static void Patch(Harmony harmony)
        {
            var lobbyUtilityType = AccessTools.TypeByName("LobbyUtility");
            if (lobbyUtilityType == null)
            {
                Plugin.Log.LogWarning("LobbyUtility type not found; skipping invite patch.");
                return;
            }

            Core.PatchNamedMethod(
                harmony,
                lobbyUtilityType,
                "CanInvite",
                prefix: AccessTools.Method(typeof(LobbyUtility), nameof(CanInvite_Prefix)));
        }

        private static bool CanInvite_Prefix(object friend, ref bool __result)
        {
            try
            {
                var playersManager = Reflect.GetStaticMemberValue("PlayersManager", "Instance");
                var activePlayers = Reflect.GetMemberValue(playersManager, "ActivePlayers");
                var activePlayerCount = Reflect.GetCount(activePlayers);
                var alreadyInGameWithHost = Reflect.GetBool(friend, "IsInGameWithHost", false);

                __result = activePlayerCount < Plugin.GetConfiguredMaxPlayers() && !alreadyInGameWithHost;
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(string.Format("LobbyUtility_CanInvite_Prefix failed: {0}", ex));
                return true;
            }
        }
    }
}
