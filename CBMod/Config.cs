using BepInEx.Configuration;

namespace CBMod
{
    // Keys are Numpad only. Values are UnityEngine.InputSystem.Key names
    // (Numpad0..Numpad9, NumpadPlus, NumpadMinus, NumpadMultiply, ...).
    internal static class Cfg
    {
        // Keys
        public static ConfigEntry<string> GodMode, Stamina, Injuries, Characters, AllData, Money, OneHit, CameraZoom, Cosmetics;
        public static ConfigEntry<string> ChainUp, ChainDown, ChainReset, Menu;

        // Menu
        public static ConfigEntry<string> Language;
        public static ConfigEntry<bool> BlockInput;

        private static ConfigFile _file;

        // Values
        public static ConfigEntry<float> DamageMultiplier, CameraZoomFactor, ChainLengthStep;
        public static ConfigEntry<int> MoneyAmount;
        public static ConfigEntry<string> ZoomMethod;

        // Save and audio
        public static ConfigEntry<string> BackupDir;
        public static ConfigEntry<bool> SaveAfterUnlock;
        public static ConfigEntry<float> BeepVolume;
        public static ConfigEntry<string> PlayMode;

        // Slider drags change values many times per second, so we save on demand (SaveOnConfigSet is off).
        public static void Save() { _file?.Save(); }

        public static void Bind(ConfigFile c)
        {
            _file = c;
            c.SaveOnConfigSet = false;

            GodMode = c.Bind("Keys", "GodMode", "Numpad1", "Toggle god mode (host only).");
            Stamina = c.Bind("Keys", "InfiniteStamina", "Numpad2", "Toggle infinite stamina (local player).");
            Injuries = c.Bind("Keys", "ClearInjuries", "Numpad3", "Clear all injuries (one-shot, local player).");
            Characters = c.Bind("Keys", "UnlockCharacters", "Numpad4", "Unlock all characters (one-shot, backs up your save first).");
            AllData = c.Bind("Keys", "UnlockAllData", "Numpad5", "Unlock everything the game's own UnlockAllData() unlocks (one-shot, backs up your save first).");
            Money = c.Bind("Keys", "InfiniteMoney", "Numpad6", "Toggle infinite money (host only, run money is shared).");
            OneHit = c.Bind("Keys", "OneHitKill", "Numpad7", "Toggle one-hit kill (multiplies your damage against enemies).");
            CameraZoom = c.Bind("Keys", "CameraZoomOut", "Numpad8", "Toggle camera zoom out (local only).");
            Cosmetics = c.Bind("Keys", "UnlockCosmetics", "Numpad9", "Unlock all cosmetics (one-shot, backs up your save first).");
            ChainUp = c.Bind("Keys", "ChainLengthIncrease", "NumpadPlus", "Increase chain length (host only).");
            ChainDown = c.Bind("Keys", "ChainLengthDecrease", "NumpadMinus", "Decrease chain length (host only).");
            ChainReset = c.Bind("Keys", "ChainLengthReset", "NumpadMultiply", "Reset chain length to the game's original value (host only).");

            Menu = c.Bind("Keys", "ToggleMenu", "Numpad0", "Open or close the in-game menu.");
            Language = c.Bind("Menu", "Language", "EN",
                new ConfigDescription("Menu and on-screen message language. Can also be changed in the menu.", new AcceptableValueList<string>("EN", "TR")));
            BlockInput = c.Bind("Menu", "BlockGameInput", true,
                "While the menu is open, disable the game's input actions so clicks and keys do not reach the game. Turn off if it causes problems.");

            DamageMultiplier = c.Bind("Values", "DamageMultiplier", 100f,
                new ConfigDescription("Damage multiplier for one-hit kill (applies to your attacks against enemies).", new AcceptableValueRange<float>(1f, 1000f)));
            CameraZoomFactor = c.Bind("Values", "CameraZoomFactor", 2.0f,
                new ConfigDescription("Camera zoom-out factor. 1 = normal, 2 = twice as far.", new AcceptableValueRange<float>(1f, 5f)));
            ZoomMethod = c.Bind("Values", "CameraZoomMethod", "Auto",
                "What the zoom scales: Auto (follows the camera's own adjustment mode) | Distance | Fov | Both. Try another one if Auto shows no change.");
            ChainLengthStep = c.Bind("Values", "ChainLengthStep", 0.5f,
                new ConfigDescription("How much one key press changes the chain length (game units).", new AcceptableValueRange<float>(0.05f, 5f)));
            MoneyAmount = c.Bind("Values", "InfiniteMoneyAmount", 99999,
                new ConfigDescription("Money is topped up to this amount while infinite money is on.", new AcceptableValueRange<int>(1, 9999999)));

            BackupDir = c.Bind("Save", "BackupDir", "",
                "Folder for save backups. Empty = Documents\\CBMod-SaveBackups.");
            SaveAfterUnlock = c.Bind("Save", "SaveAfterUnlock", true,
                "Call PersistentData.Save() after an unlock. A backup is always taken first.");

            BeepVolume = c.Bind("Audio", "BeepVolume", 0.5f, "Beep volume (0-1).");
            PlayMode = c.Bind("Audio", "PlayMode", "Play", "How beeps are played: Play | OneShot | AtPoint. Try another one if you hear nothing.");

            c.Save();
        }
    }
}
