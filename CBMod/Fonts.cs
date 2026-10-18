using System;
using UnityEngine;

namespace CBMod
{
    // IMGUI draws with a built-in font. This checks that it has the Turkish letters and, if it does not,
    // swaps in an operating-system font that does (Segoe UI / Arial / Tahoma).
    internal static class Fonts
    {
        private const string TurkishChars = "ĞğŞşİıÇçÖöÜü";

        public static Font Active;        // font we switched to (null = the default one is fine)
        public static bool TurkishOk;     // all Turkish letters can be drawn
        private static bool _done;

        // Needs GUI.skin, which is only safe to touch from OnGUI, so this runs lazily on the first OnGUI call.
        public static void EnsureInitialised()
        {
            if (_done) return;
            _done = true;
            try
            {
                Font f = GUI.skin.font;
                if (f == null)
                {
                    try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                }
                string defaultName = f != null ? f.name : "(default)";
                bool ok = f != null && Has(f, out string missingDefault);
                Plugin.L.LogInfo($"Font check: default font '{defaultName}' Turkish letters {(ok ? "OK" : "MISSING")}");

                if (!ok)
                {
                    foreach (var name in new[] { "Segoe UI", "Arial", "Tahoma" })
                    {
                        var os = Font.CreateDynamicFontFromOSFont(name, 16);
                        if (os != null && Has(os, out _))
                        {
                            os.hideFlags = HideFlags.HideAndDontSave;
                            GUI.skin.font = os;
                            Active = os;
                            ok = true;
                            Plugin.L.LogInfo($"Font check: switched IMGUI to OS font '{name}', Turkish letters OK");
                            break;
                        }
                        Plugin.L.LogInfo($"Font check: OS font '{name}' is not usable");
                    }
                    if (!ok) Plugin.L.LogWarning("Font check: no font with all Turkish letters found; some letters may be drawn as boxes.");
                }
                TurkishOk = ok;
            }
            catch (Exception e)
            {
                Plugin.L.LogError("Font check failed: " + e);
            }
        }

        private static bool Has(Font f, out string missing)
        {
            missing = "";
            foreach (char c in TurkishChars)
                if (!f.HasCharacter(c)) missing += c;
            return missing.Length == 0;
        }

        public static string TestText => TurkishChars;
    }
}
