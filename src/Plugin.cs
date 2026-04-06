using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace PluginNamespace;

[BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
public class Plugin : BasePlugin
{
    public static new ManualLogSource Logger { get; private set; } = null!;
    public static Harmony? Harmony { get; private set; }

    public override void Load()
    {
        Logger = base.Logger;
        Harmony = new Harmony(PluginInfo.GUID);
        Harmony.PatchAll();
        Logger.LogInfo($"Loaded {PluginInfo.GUID}");
    }

    public override void Unload()
    {
        Harmony?.UnpatchAll();
        base.Unload();
    }
}

internal static class PluginInfo
{
    public const string GUID = "PluginNamespace.Plugin";
    public const string Name = "PluginName";
    public const string Version = "1.0.0";
}
