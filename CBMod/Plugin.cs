using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace CBMod
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        public const string Guid = "org.methsploit.cbmod";
        public const string Name = "CBMod";
        public const string Version = BuildInfo.Version;   // generated from <Version> in CBMod.csproj

        internal static ManualLogSource L;

        public override void Load()
        {
            L = base.Log;

            // IL2CPP: our own MonoBehaviour has to be registered with the game's runtime first.
            ClassInjector.RegisterTypeInIl2Cpp<ModBehaviour>();

            var go = new GameObject("CBMod");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<ModBehaviour>();

            Cfg.Bind(Config);
            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            L.LogInfo($"{Name} {Version} loaded. Numpad1 god mode (host only), 2 stamina, 3 injuries, 4 characters, 9 cosmetics.");
        }
    }
}
