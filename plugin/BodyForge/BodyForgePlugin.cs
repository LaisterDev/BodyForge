// BodyForge — BepInEx plugin entry point.
using BepInEx;
using HarmonyLib;

namespace BodyForge
{
    [BepInPlugin(PluginInfo.Id, PluginInfo.Name, PluginInfo.Version)]
    public class BodyForgePlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Instance = this;
            Harmony harmony = new Harmony(PluginInfo.Id);
            harmony.PatchAll();
            Logger.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} loaded");
        }

        private void Update()
        {
            LiveBridge.Tick();
            MultiplayerSync.Tick();
        }

        public static BodyForgePlugin Instance { get; private set; }

        public static void LogInfo(string msg) => Instance?.Logger?.LogInfo(msg);

        public static void LogWarn(string msg) => Instance?.Logger?.LogWarning(msg);

        public static void LogError(string msg) => Instance?.Logger?.LogError(msg);
    }

    public static class PluginInfo
    {
        public const string Id = "com.bodyforge.characterprops";
        public const string Name = "BodyForge Character Proportions";
        public const string Version = "0.4.0";
    }
}
