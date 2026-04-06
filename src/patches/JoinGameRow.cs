using System;
using System.Reflection;
using HarmonyLib;

namespace LumaPlayerLimit.Patches
{
    internal static class JoinGameRow
    {
        public static void Patch(Harmony harmony)
        {
            var joinGameRowType = AccessTools.TypeByName("JoinGameRow");
            if (joinGameRowType == null)
            {
                Plugin.Log.LogWarning("JoinGameRow type not found; skipping join row patches.");
                return;
            }

            Core.PatchNamedMethod(
                harmony,
                joinGameRowType,
                "OnJoinGame",
                prefix: AccessTools.Method(typeof(JoinGameRow), nameof(OnJoinGame_Prefix)));

            Core.PatchNamedMethod(
                harmony,
                joinGameRowType,
                "Initialize",
                postfix: AccessTools.Method(typeof(JoinGameRow), nameof(Initialize_Postfix)));
        }

        private static bool OnJoinGame_Prefix(object __instance)
        {
            try
            {
                var lobby = Reflect.GetMemberValue(__instance, "m_lobby");
                if (lobby == null)
                {
                    return true;
                }

                if (Reflect.GetBool(lobby, "IsVersionMismatch", false))
                {
                    return false;
                }

                if (Reflect.InvokeIfExists(lobby, "JoinLobby"))
                {
                    return false;
                }

                Plugin.Log.LogWarning("JoinGameRow.OnJoinGame: m_lobby.JoinLobby() was not found.");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(string.Format("JoinGameRow_OnJoinGame_Prefix failed: {0}", ex));
                return true;
            }
        }

        private static void Initialize_Postfix(object __instance, object lobbyInfo)
        {
            try
            {
                if (lobbyInfo == null)
                {
                    return;
                }

                var playersText = Reflect.GetMemberValue(__instance, "m_playersInGameText");
                var joinButton = Reflect.GetMemberValue(__instance, "m_joinGameButton");

                var playersInGame = Reflect.GetInt(lobbyInfo, "PlayersInGame", 0);
                var isVersionMismatch = Reflect.GetBool(lobbyInfo, "IsVersionMismatch", false);

                Reflect.SetMemberValue(playersText, "text", string.Format("{0} / {1}", playersInGame, Plugin.GetConfiguredMaxPlayers()));
                Reflect.SetMemberValue(joinButton, "Interactable", !isVersionMismatch);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(string.Format("JoinGameRow_Initialize_Postfix failed: {0}", ex));
            }
        }
    }
}
