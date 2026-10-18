using System;
using System.Collections.Generic;
using UnityEngine;

namespace CBMod
{
    // One-shot actions. true = done (confirm sound), false = could not be done (deny sound).
    // NoteKey is an optional localization key of an on-screen message that overrides the default one.
    internal static class Actions
    {
        public static string NoteKey;

        public static bool ClearInjuries()
        {
            var players = Features.LocalPlayers();
            if (players.Count == 0) { Plugin.L.LogWarning("Clear injuries: no local player."); return false; }
            foreach (var p in players)
            {
                var pd = p.PlayerData;
                if (pd == null) continue;
                pd.RemoveAllInjuries();
                p.RefreshInjuryCache();   // refresh the injury effects the player has cached
            }
            Plugin.L.LogInfo($"Injuries cleared ({players.Count} local player(s)).");
            return true;
        }

        public static bool UnlockCharacters()
        {
            // Back up first; if that fails, do not touch anything.
            if (!SaveBackup.Run(out _)) return false;
            // The game's own method: changes PersistentData.Current in memory.
            PersistentData.UnlockAllBeasts();
            Plugin.L.LogInfo("PersistentData.UnlockAllBeasts() called (in memory).");
            SaveNow("characters");
            return true;
        }

        // The game's own UnlockAllData(). We compare a snapshot of the save data before and after,
        // so we can tell the player when there was nothing left to unlock.
        public static bool UnlockAllData()
        {
            var data = PersistentData.Current;
            if (data == null) { Plugin.L.LogWarning("Unlock all data: save data not available."); return false; }
            if (!SaveBackup.Run(out _)) return false;

            string before = Snapshot(data);
            PersistentData.UnlockAllData();
            string after = Snapshot(data);
            Plugin.L.LogInfo("Unlock all data, before: " + before);
            Plugin.L.LogInfo("Unlock all data, after:  " + after);

            if (before == after)
            {
                NoteKey = "msg.nothing_to_unlock";
                Plugin.L.LogInfo("UnlockAllData changed nothing: everything was already unlocked.");
                return true;
            }
            SaveNow("all data");
            return true;
        }

        private static string Snapshot(PersistentData.Data d)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("beasts=").Append(d.BeastsUnlocked?.Count ?? -1);
            sb.Append(" pickups=").Append(d.PickupsUnlocked?.Count ?? -1);
            sb.Append(" codex[beast/injury/skill/purchase/pickup/enemy/surgery/achievement]=")
              .Append(d.BeastCodexEntries?.Count ?? -1).Append('/')
              .Append(d.InjuryCodexEntries?.Count ?? -1).Append('/')
              .Append(d.SkillCodexEntries?.Count ?? -1).Append('/')
              .Append(d.PurchaseCodexEntries?.Count ?? -1).Append('/')
              .Append(d.PickupCodexEntries?.Count ?? -1).Append('/')
              .Append(d.EnemyCodexEntries?.Count ?? -1).Append('/')
              .Append(d.SurgeryCodexEntries?.Count ?? -1).Append('/')
              .Append(d.AchievementCodexEntries?.Count ?? -1);
            sb.Append(" beastLevels=");
            foreach (BeastType bt in Enum.GetValues(typeof(BeastType)))
                if (bt != BeastType.None) sb.Append(d.GetBeastLevel(bt)).Append(',');
            return sb.ToString();
        }

        // Cosmetics are not a separate list in the save: each one is the reward of an achievement
        // (AchievementDef.cosmeticUnlock), so "cosmetic unlocked" means "achievement completed".
        // We only raise the achievement progress stored in the save's codex to its target.
        // Platform.UpdateAchievement (real Steam achievements) is NOT called: your Steam profile is untouched.
        public static bool UnlockCosmetics()
        {
            if (!GameLibrary.InstanceExists) { Plugin.L.LogWarning("Cosmetics: GameLibrary not available (try again in the menu or in a run)."); return false; }
            var defs = GameLibrary.Instance.AchievementDefs;
            var data = PersistentData.Current;
            if (defs == null || data == null) { Plugin.L.LogWarning("Cosmetics: achievement list or save data not available."); return false; }

            if (!SaveBackup.Run(out _)) return false;

            int total = 0, already = 0, changed = 0, failed = 0;
            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                if (d == null || d.cosmeticUnlock == CosmeticType.None) continue;
                total++;
                if (data.IsAchievementUnlocked(d)) { already++; continue; }

                var cx = data.GetAchievementCodex(d);
                if (cx == null) { failed++; continue; }
                cx.progress = Math.Max(Math.Max(d.target, d.requiredValue), 1);
                if (data.IsAchievementUnlocked(d)) changed++;
                else { failed++; Plugin.L.LogWarning($"Cosmetics: {d.GetAchievementID()} progress={cx.progress} but still locked (target={d.target}, required={d.requiredValue})."); }
            }
            Plugin.L.LogInfo($"Cosmetic achievements: total={total} alreadyUnlocked={already} unlocked={changed} failed={failed}");
            if (changed > 0)
            {
                try { data.OnDataChanged(); } catch (Exception e) { Plugin.L.LogWarning("OnDataChanged: " + e.Message); }
                SaveNow("cosmetics");
            }
            return failed == 0;
        }

        // ---- Chain (host only; the caller checks Features.IsHost()) ----

        // Original rope length per chain, remembered the first time we change it.
        private static readonly Dictionary<int, float> OriginalLength = new Dictionary<int, float>();

        // delta: change in game units. Returns false if there is no chain.
        public static bool ChangeChainLength(float delta)
        {
            if (!ChainManager.InstanceExists) { Plugin.L.LogWarning("Chain: ChainManager not available."); return false; }
            var chains = ChainManager.Instance.chains;
            if (chains == null || chains.Count == 0) { Plugin.L.LogWarning("Chain: no active chain."); return false; }
            for (int i = 0; i < chains.Count; i++)
            {
                var c = chains[i];
                if (c == null) continue;
                int id = c.GetInstanceID();
                if (!OriginalLength.ContainsKey(id)) OriginalLength[id] = c.ropeLength;
                float next = Mathf.Max(0.5f, c.ropeLength + delta);
                float requested = next;
                c.SetLength(next);
                // SetLength does not necessarily store the value in ropeLength, so log every length-related field.
                Plugin.L.LogInfo($"Chain {i}: requested {requested} (original ropeLength {OriginalLength[id]}) -> now ropeLength={c.ropeLength} " +
                                 $"currentRopeLength={c.CurrentRopeLength} maxLengthThreshold={c.maxLengthThreshold} tautThreshold={c.tautThreshold} " +
                                 $"taut={c.IsTaut} maxReached={c.IsMaxLengthReached}");
            }
            return true;
        }

        public static bool ResetChainLength()
        {
            if (!ChainManager.InstanceExists) { Plugin.L.LogWarning("Chain: ChainManager not available."); return false; }
            var chains = ChainManager.Instance.chains;
            if (chains == null || chains.Count == 0) { Plugin.L.LogWarning("Chain: no active chain."); return false; }
            int reset = 0;
            for (int i = 0; i < chains.Count; i++)
            {
                var c = chains[i];
                if (c == null) continue;
                if (OriginalLength.TryGetValue(c.GetInstanceID(), out var orig))
                {
                    c.SetLength(orig);
                    reset++;
                    Plugin.L.LogInfo($"Chain {i}: rope length reset to {orig}");
                }
            }
            if (reset == 0) NoteKey = "msg.chain_original";
            return true;
        }

        // Current rope length of the first chain (used by the menu slider). False if there is no chain.
        public static bool TryGetChainLength(out float length, out float original)
        {
            length = original = 0f;
            if (!ChainManager.InstanceExists) return false;
            var chains = ChainManager.Instance.chains;
            if (chains == null || chains.Count == 0 || chains[0] == null) return false;
            var c = chains[0];
            length = c.ropeLength;
            original = OriginalLength.TryGetValue(c.GetInstanceID(), out var o) ? o : c.ropeLength;
            return true;
        }

        // Sets an absolute rope length on every chain (menu slider). Host only: the caller checks Features.IsHost().
        public static void SetChainLength(float value, bool log)
        {
            if (!ChainManager.InstanceExists) return;
            var chains = ChainManager.Instance.chains;
            if (chains == null) return;
            for (int i = 0; i < chains.Count; i++)
            {
                var c = chains[i];
                if (c == null) continue;
                int id = c.GetInstanceID();
                if (!OriginalLength.ContainsKey(id)) OriginalLength[id] = c.ropeLength;
                c.SetLength(Mathf.Max(0.5f, value));
                if (log)
                    Plugin.L.LogInfo($"Chain {i}: slider set {value} -> ropeLength={c.ropeLength} currentRopeLength={c.CurrentRopeLength} maxLengthThreshold={c.maxLengthThreshold}");
            }
        }

        // Called after the backup was taken. Steam Cloud may write an older save back (see README).
        private static void SaveNow(string what)
        {
            if (!Cfg.SaveAfterUnlock.Value) { Plugin.L.LogInfo($"Save skipped (config): {what}"); return; }
            try
            {
                PersistentData.Save();
                Plugin.L.LogInfo($"PersistentData.Save() called ({what}). Note: Steam Cloud may restore an older save.");
            }
            catch (Exception e) { Plugin.L.LogError("PersistentData.Save failed: " + e.Message); }
        }
    }
}
