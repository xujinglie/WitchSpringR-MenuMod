using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace WSMenuMod
{
    [BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BasePlugin
    {
        internal static ManualLogSource LogSource;

        public override void Load()
        {
            LogSource = Log;
            Log.LogInfo($"{PluginInfo.Name} v{PluginInfo.Version} 正在加载...");
            AddComponent<MenuBehaviour>();
            Log.LogInfo("菜单已注册，按 F8 打开/关闭。");
        }
    }

    internal static class PluginInfo
    {
        public const string Guid = "witchspringr.modmenu";
        public const string Name = "WitchSpring R Menu Mod";
        public const string Version = "1.2.0";
    }
}
