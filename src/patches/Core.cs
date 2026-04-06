using System;
using System.Reflection;
using HarmonyLib;

namespace LumaPlayerLimit.Patches
{
    internal static class Core
    {
        public static void PatchNamedMethod(
            Harmony harmony,
            Type type,
            string methodName,
            MethodInfo prefix = null,
            MethodInfo postfix = null,
            MethodInfo transpiler = null)
        {
            var original = AccessTools.Method(type, methodName);
            if (original == null)
            {
                Plugin.Log.LogWarning(string.Format("{0}.{1} not found.", type.FullName, methodName));
                return;
            }

            try
            {
                harmony.Patch(
                    original,
                    prefix: prefix != null ? new HarmonyMethod(prefix) : null,
                    postfix: postfix != null ? new HarmonyMethod(postfix) : null,
                    transpiler: transpiler != null ? new HarmonyMethod(transpiler) : null);

                Plugin.Log.LogInfo(string.Format("Patched {0}.{1}", type.FullName, methodName));
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(string.Format("Failed to patch {0}.{1}: {2}", type.FullName, methodName, ex));
            }
        }
    }
}
