using System.Collections.Generic;

namespace CBMod
{
    // Every text the player can see (menu labels, on-screen notifications, warnings) lives in this table:
    // key -> { English, Turkish }. Log messages and config descriptions are not translated on purpose
    // (they stay English so bug reports are readable). To add a language, add a column here and a case in Loc.Index.
    internal static class Loc
    {
        private const int EN = 0, TR = 1;

        private static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            // ---- features (checkbox labels and notification names) ----
            ["feat.god"] = new[] { "God Mode", "Ölümsüzlük" },
            ["feat.stamina"] = new[] { "Infinite Stamina", "Sınırsız Dayanıklılık" },
            ["feat.money"] = new[] { "Infinite Money", "Sınırsız Para" },
            ["feat.onehit"] = new[] { "One-Hit Kill", "Tek Vuruşta Öldürme" },
            ["feat.zoom"] = new[] { "Camera Zoom Out", "Kamera Uzaklaştırma" },

            // ---- sliders ----
            ["slider.damage"] = new[] { "Damage Multiplier", "Hasar Çarpanı" },
            ["slider.zoom"] = new[] { "Camera Zoom", "Kamera Uzaklığı" },
            ["slider.chain"] = new[] { "Chain Length", "Zincir Uzunluğu" },

            // ---- buttons ----
            ["btn.injuries"] = new[] { "Clear Injuries", "Yaraları Temizle" },
            ["btn.chars"] = new[] { "Unlock Characters", "Karakterlerin Kilidini Aç" },
            ["btn.cosm"] = new[] { "Unlock Cosmetics", "Kostümlerin Kilidini Aç" },
            ["btn.alldata"] = new[] { "Unlock All Data", "Tüm Verilerin Kilidini Aç" },
            ["btn.reset"] = new[] { "Reset", "Sıfırla" },

            // ---- menu chrome ----
            ["menu.title"] = new[] { "CBMod", "CBMod" },
            ["section.toggles"] = new[] { "Toggles", "Seçenekler" },
            ["section.values"] = new[] { "Values", "Değerler" },
            ["section.actions"] = new[] { "One-time actions", "Tek seferlik işlemler" },
            ["label.hostonly"] = new[] { "Host only", "Sadece host" },
            ["label.language"] = new[] { "Language", "Dil" },
            ["label.nochain"] = new[] { "no chain", "zincir yok" },
            ["label.chartest"] = new[] { "Character test", "Karakter testi" },
            ["label.chartest_ok"] = new[] { "OK", "Tamam" },
            ["label.chartest_bad"] = new[] { "glyphs missing", "eksik karakter var" },
            ["label.close_hint"] = new[] { "{0}: close menu", "{0}: menüyü kapat" },
            ["status.host"] = new[] { "Host", "Host" },
            ["status.guest"] = new[] { "Guest", "Misafir" },
            ["state.on"] = new[] { "ON", "AÇIK" },
            ["state.off"] = new[] { "OFF", "KAPALI" },

            // ---- on-screen notifications ----
            ["msg.god_host_only"] = new[] { "God mode: host only", "Ölümsüzlük: sadece host" },
            ["msg.money_host_only"] = new[] { "Infinite money: host only", "Sınırsız para: sadece host" },
            ["msg.chain_host_only"] = new[] { "Chain length: host only", "Zincir uzunluğu: sadece host" },
            ["msg.injuries_ok"] = new[] { "Injuries cleared", "Yaralar temizlendi" },
            ["msg.injuries_fail"] = new[] { "Could not clear injuries", "Yaralar temizlenemedi" },
            ["msg.chars_ok"] = new[] { "Characters unlocked (save backed up)", "Karakterler açıldı (kayıt yedeklendi)" },
            ["msg.alldata_ok"] = new[] { "All data unlocked (save backed up)", "Tüm veriler açıldı (kayıt yedeklendi)" },
            ["msg.unlock_fail"] = new[] { "Unlock cancelled (backup or error, see log)", "Kilit açma iptal edildi (yedek veya hata, log'a bak)" },
            ["msg.cosm_ok"] = new[] { "Cosmetics unlocked (save backed up)", "Kostümler açıldı (kayıt yedeklendi)" },
            ["msg.cosm_fail"] = new[] { "Cosmetics not fully unlocked (see log)", "Kostümler tam açılamadı (log'a bak)" },
            ["msg.nothing_to_unlock"] = new[] { "Nothing left to unlock (already unlocked)", "Açılacak bir şey kalmadı (zaten açık)" },
            ["msg.chain_up"] = new[] { "Chain length increased", "Zincir uzadı" },
            ["msg.chain_down"] = new[] { "Chain length decreased", "Zincir kısaldı" },
            ["msg.chain_reset"] = new[] { "Chain length reset", "Zincir sıfırlandı" },
            ["msg.chain_original"] = new[] { "Chain length is already original", "Zincir zaten orijinal uzunlukta" },
            ["msg.chain_none"] = new[] { "No chain found (see log)", "Zincir bulunamadı (log'a bak)" },
            ["msg.zoom_applied"] = new[] { "Camera zoom: applied distance = {0} ({1})", "Kamera uzaklığı: {0} ({1})" },
        };

        private static int Index
        {
            get
            {
                var v = Cfg.Language != null ? Cfg.Language.Value : "EN";
                return string.Equals(v, "TR", System.StringComparison.OrdinalIgnoreCase) ? TR : EN;
            }
        }

        public static string T(string key)
        {
            return Table.TryGetValue(key, out var v) ? v[Index] : key;   // a missing key shows the key itself
        }

        public static string F(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        public static bool IsTurkish => Index == TR;
    }
}
