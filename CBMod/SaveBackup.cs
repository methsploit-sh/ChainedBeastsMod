using System;
using System.IO;
using UnityEngine;

namespace CBMod
{
    // Called before anything that can touch the save. If the backup fails, the action is not performed.
    internal static class SaveBackup
    {
        private static readonly string[] Files = { "SaveData", "SaveDataPrivate", "Settings" };

        public static bool Run(out string dest)
        {
            dest = null;
            try
            {
                string src = Application.persistentDataPath;   // ...\LocalLow\Featherweight Games\ChainedBeasts
                string root = Cfg.BackupDir.Value;
                if (string.IsNullOrWhiteSpace(root))
                    root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CBMod-SaveBackups");
                dest = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
                Directory.CreateDirectory(dest);

                int copied = 0;
                foreach (var f in Files)
                {
                    string p = Path.Combine(src, f);
                    if (!File.Exists(p)) continue;
                    File.Copy(p, Path.Combine(dest, f), false);
                    copied++;
                }
                if (copied == 0) { Plugin.L.LogError("Backup: no save files found in " + src); return false; }
                Plugin.L.LogInfo($"Save backup created ({copied} files): {dest}");
                return true;
            }
            catch (Exception e)
            {
                Plugin.L.LogError("Save backup FAILED, action cancelled: " + e.Message);
                return false;
            }
        }
    }
}
