using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CBMod
{
    // The in-game menu (IMGUI). Every control calls the same Controller methods as the Numpad hotkeys,
    // so the menu and the hotkeys always show and change the same state.
    //
    // IL2CPP strips methods nobody used at build time, and GUILayout.FlexibleSpace() turned out to be one of them.
    // So this menu does not use GUILayout at all: every control is placed by hand with a Rect (GUI.Label, GUI.Button,
    // GUI.Toggle, GUI.HorizontalSlider, GUI.Box). On the first OnGUI each of those is test-called once and the
    // result is logged; Toggle and Slider have hand-made fallbacks in case they turn out to be stripped too.
    internal static class Hud
    {
        public static bool Open;

        private static CursorLockMode _prevLock;
        private static bool _prevVisible;
        private static readonly List<InputAction> Blocked = new List<InputAction>();

        private static bool _cfgDirty;        // a config value was changed by a slider, save on mouse release
        private static bool _chainDragging;   // the chain slider moved, log the final value on mouse release
        private static bool _errorLogged;     // the draw error is logged once, not every frame

        // What the probe found. Label/Button/Box are required; Toggle/Slider have fallbacks.
        private static bool _probed, _canDraw, _hasToggle, _hasSlider;

        // Manual layout state for the frame being drawn.
        private const float PadX = 18f, RowH = 30f;
        private static float _x, _y, _w;

        // ---------- open / close ----------

        public static void Toggle()
        {
            if (Open) Close(); else OpenMenu();
        }

        private static void OpenMenu()
        {
            if (_probed && !_canDraw)
            {
                Plugin.L.LogWarning("Menu: basic IMGUI methods are unavailable in this build, the menu cannot be shown (see the probe lines above).");
                return;
            }
            Open = true;
            _prevLock = Cursor.lockState;
            _prevVisible = Cursor.visible;
            KeepCursorFree();
            if (Cfg.BlockInput.Value) BlockGameInput();
            Plugin.L.LogInfo("Menu opened.");
        }

        // Safe to call at any time (also after a draw error): always gives the cursor and the game input back.
        private static void Close()
        {
            bool wasOpen = Open;
            Open = false;
            try { UnblockGameInput(); }
            catch (Exception e) { Plugin.L.LogWarning("Menu: unblocking input failed: " + e.Message); }
            try
            {
                if (wasOpen)
                {
                    Cursor.lockState = _prevLock;       // give the cursor back exactly as the game had it
                    Cursor.visible = _prevVisible;
                }
            }
            catch (Exception e) { Plugin.L.LogWarning("Menu: restoring cursor failed: " + e.Message); }
            if (_cfgDirty) { Cfg.Save(); _cfgDirty = false; }
            _chainDragging = false;
            if (wasOpen) Plugin.L.LogInfo("Menu closed.");
        }

        // The game tries to hide and lock the cursor every frame, so this is called from Update and LateUpdate.
        public static void KeepCursorFree()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Disables every input action that is enabled right now and remembers which ones, to re-enable them on close.
        // Our own hotkeys read the keyboard directly, so they keep working.
        private static void BlockGameInput()
        {
            try
            {
                Blocked.Clear();
                var enabled = InputSystem.ListEnabledActions();
                for (int i = 0; i < enabled.Count; i++)
                {
                    var a = enabled[i];
                    if (a == null) continue;
                    a.Disable();
                    Blocked.Add(a);
                }
                Plugin.L.LogInfo($"Menu: disabled {Blocked.Count} game input action(s) while the menu is open.");
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning("Menu: could not block game input: " + e.Message);
            }
        }

        private static void UnblockGameInput()
        {
            int n = 0;
            foreach (var a in Blocked)
            {
                try { if (a != null) { a.Enable(); n++; } }
                catch (Exception e) { Plugin.L.LogWarning("Menu: could not re-enable an input action: " + e.Message); }
            }
            if (Blocked.Count > 0) Plugin.L.LogInfo($"Menu: re-enabled {n} of {Blocked.Count} game input action(s).");
            Blocked.Clear();
        }

        // ---------- IMGUI availability probe ----------

        private static bool Try(string name, Action call)
        {
            try { call(); Plugin.L.LogInfo($"IMGUI probe: {name} OK"); return true; }
            catch (Exception e)
            {
                Plugin.L.LogWarning($"IMGUI probe: {name} UNAVAILABLE ({e.GetType().Name}: {e.Message})");
                return false;
            }
        }

        // Test-calls every IMGUI member the menu uses, once, from inside OnGUI. Harmless: everything is drawn off screen.
        public static void ProbeOnce()
        {
            if (_probed) return;
            _probed = true;
            var off = new Rect(-200f, -200f, 10f, 10f);

            bool label = Try("GUI.Label", () => GUI.Label(off, "x"));
            bool box = Try("GUI.Box", () => GUI.Box(off, ""));
            bool button = Try("GUI.Button", () => GUI.Button(off, "x"));
            _hasToggle = Try("GUI.Toggle", () => GUI.Toggle(off, false, "x"));
            _hasSlider = Try("GUI.HorizontalSlider", () => GUI.HorizontalSlider(off, 0f, 0f, 1f));
            bool enabled = Try("GUI.enabled get/set", () => { var e = GUI.enabled; GUI.enabled = e; });
            bool color = Try("GUI.color get/set", () => { var c = GUI.color; GUI.color = c; });
            bool matrix = Try("GUI.matrix get/set", () => { var m = GUI.matrix; GUI.matrix = m; });
            bool skin = Try("GUI.skin label/toggle/button fontSize get/set", () =>
            {
                var s = GUI.skin; s.label.fontSize = s.label.fontSize; s.toggle.fontSize = s.toggle.fontSize; s.button.fontSize = s.button.fontSize;
            });
            bool ev = Try("Event.current type/mousePosition", () => { var e = Event.current; var t = e.type; var p = e.mousePosition; });
            Try("GUI.DrawTexture (not used by the menu)", () => GUI.DrawTexture(off, Texture2D.whiteTexture));

            _canDraw = label && box && button && enabled && color && matrix && skin && ev;
            Plugin.L.LogInfo($"IMGUI probe summary: canDraw={_canDraw} nativeToggle={_hasToggle} nativeSlider={_hasSlider} " +
                             "(GUILayout is not used: FlexibleSpace is stripped in this build)");
            if (!_canDraw)
                Plugin.L.LogError("IMGUI probe: a required IMGUI method is stripped, so the in-game menu is disabled. Hotkeys still work.");
        }

        // ---------- drawing ----------

        public static void Draw()
        {
            if (!Open) return;
            if (!_canDraw) { Close(); return; }

            var skin = GUI.skin;
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            bool oldEnabled = GUI.enabled;
            int oldLabel = skin.label.fontSize, oldToggle = skin.toggle.fontSize, oldButton = skin.button.fontSize;
            try
            {
                // Scale the whole menu with the screen height so it stays readable at 1440p / 4K.
                float s = Mathf.Clamp(Screen.height / 1080f, 1f, 2.5f);
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
                skin.label.fontSize = 16; skin.toggle.fontSize = 16; skin.button.fontSize = 16;
                GUI.enabled = true;

                float sw = Screen.width / s, sh = Screen.height / s;
                float w = 600f, h = 600f;
                float left = (sw - w) / 2f, top = (sh - h) / 2f;
                GUI.Box(new Rect(left, top, w, h), "");
                GUI.Box(new Rect(left, top, w, h), "");   // twice: the default box is see-through, this makes it readable

                _x = left + PadX; _y = top + 12f; _w = w - 2f * PadX;
                DrawContents();

                // Slider drags are committed when the mouse is released.
                if (Event.current.type == EventType.MouseUp)
                {
                    if (_chainDragging)
                    {
                        _chainDragging = false;
                        if (Actions.TryGetChainLength(out var len, out _)) Actions.SetChainLength(len, true);
                    }
                    if (_cfgDirty) { Cfg.Save(); _cfgDirty = false; }
                }
            }
            catch (Exception e)
            {
                // Log once, close the menu, and make sure the game gets its input and cursor back.
                if (!_errorLogged) { _errorLogged = true; Plugin.L.LogError("Menu draw error (menu closed): " + e); }
                Close();
            }
            finally
            {
                // Nothing in here needs a matching End call, but the shared GUI state must always be put back.
                try
                {
                    skin.label.fontSize = oldLabel; skin.toggle.fontSize = oldToggle; skin.button.fontSize = oldButton;
                    GUI.enabled = oldEnabled;
                    GUI.color = oldColor;
                    GUI.matrix = oldMatrix;
                }
                catch { /* nothing sensible left to do */ }
            }
        }

        private static Rect Row(float height)
        {
            var r = new Rect(_x, _y, _w, height);
            _y += height;
            return r;
        }

        private static void DrawContents()
        {
            bool host = Features.IsHost();

            // Title (left) and host status (right).
            var title = Row(RowH);
            GUI.Label(new Rect(title.x, title.y, 300f, title.height), Loc.T("menu.title") + " " + Plugin.Version);
            GUI.color = host ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.85f, 0.4f);
            GUI.Label(new Rect(title.xMax - 160f, title.y, 160f, title.height), Loc.T(host ? "status.host" : "status.guest"));
            GUI.color = Color.white;

            // Language selector (saved to the config straight away).
            var lang = Row(RowH + 6f);
            GUI.Label(new Rect(lang.x, lang.y, 200f, RowH), Loc.T("label.language"));
            if (GUI.Button(new Rect(lang.xMax - 150f, lang.y, 70f, RowH - 2f), Loc.IsTurkish ? "EN" : "[EN]")) SetLanguage("EN");
            if (GUI.Button(new Rect(lang.xMax - 75f, lang.y, 70f, RowH - 2f), Loc.IsTurkish ? "[TR]" : "TR")) SetLanguage("TR");

            Section("section.toggles");
            if (Check("feat.god", Features.GodMode, Cfg.GodMode.Value, true)) Controller.DoGod();
            if (Check("feat.stamina", Features.InfiniteStamina, Cfg.Stamina.Value, false)) Controller.DoStamina();
            if (Check("feat.money", Features.InfiniteMoney, Cfg.Money.Value, true)) Controller.DoMoney();
            if (Check("feat.onehit", Features.OneHitKill, Cfg.OneHit.Value, false)) Controller.DoOneHit();
            if (Check("feat.zoom", Features.CameraZoomOut, Cfg.CameraZoom.Value, false)) Controller.DoZoom();

            Section("section.values");
            var dmg = SliderRow("slider.damage", Cfg.DamageMultiplier.Value, 1f, 1000f, "F0");
            if (dmg.changed) { Cfg.DamageMultiplier.Value = Mathf.Round(dmg.value); _cfgDirty = true; }

            var zoom = SliderRow("slider.zoom", Cfg.CameraZoomFactor.Value, 1f, 5f, "F1");
            if (zoom.changed) { Cfg.CameraZoomFactor.Value = Mathf.Round(zoom.value * 10f) / 10f; _cfgDirty = true; }

            ChainRow(host);

            Section("section.actions");
            ButtonPair("btn.injuries", 0, "btn.chars", 1);
            ButtonPair("btn.cosm", 3, "btn.alldata", 2);

            // Turkish character check row (the font check result is also in the log), then the close hint.
            _y += 6f;
            GUI.Label(Row(RowH), $"{Loc.T("label.chartest")}: {Fonts.TestText}  [{Loc.T(Fonts.TurkishOk ? "label.chartest_ok" : "label.chartest_bad")}]");
            GUI.Label(Row(RowH), Loc.F("label.close_hint", KeyLabel(Cfg.Menu.Value)));
        }

        private static void SetLanguage(string code)
        {
            if (string.Equals(Cfg.Language.Value, code, StringComparison.OrdinalIgnoreCase)) return;
            Cfg.Language.Value = code;
            Cfg.Save();
            Plugin.L.LogInfo("Language set to " + code);
        }

        private static void Section(string key)
        {
            _y += 6f;
            GUI.Label(Row(RowH - 2f), "-- " + Loc.T(key) + " --");
        }

        // A checkbox row with the hotkey in brackets. Returns true when the user clicked it.
        // Host-only rows are greyed out and unclickable for guests, but still show the "Host only" tag.
        private static bool Check(string key, bool value, string hotkey, bool hostOnly)
        {
            var r = Row(RowH);
            bool wasEnabled = GUI.enabled;
            if (hostOnly && !Features.IsHost()) GUI.enabled = false;

            var boxRect = new Rect(r.x, r.y, r.width - (hostOnly ? 150f : 0f), r.height - 2f);
            string text = $"{Loc.T(key)}  [{KeyLabel(hotkey)}]";
            bool v;
            if (_hasToggle) v = GUI.Toggle(boxRect, value, text);
            else v = GUI.Button(boxRect, (value ? "[x] " : "[  ] ") + text) ? !value : value;   // fallback if Toggle is stripped
            if (hostOnly) GUI.Label(new Rect(r.xMax - 150f, r.y, 150f, r.height), Loc.T("label.hostonly"));

            GUI.enabled = wasEnabled;
            return v != value;
        }

        private static (float value, bool changed) SliderRow(string key, float value, float min, float max, string fmt)
        {
            var r = Row(RowH + 4f);
            GUI.Label(new Rect(r.x, r.y, 190f, RowH), Loc.T(key));
            float nv = DoSlider(key, new Rect(r.x + 195f, r.y + 8f, 230f, 20f), value, min, max);
            GUI.Label(new Rect(r.x + 435f, r.y, 80f, RowH), nv.ToString(fmt));
            return (nv, Mathf.Abs(nv - value) > 0.0001f);
        }

        // Chain length: host only. The slider shows the real rope length, so hotkey changes show up here too.
        private static void ChainRow(bool host)
        {
            bool have = Actions.TryGetChainLength(out float len, out float orig);
            var r = Row(RowH + 4f);
            bool wasEnabled = GUI.enabled;
            if (!host || !have) GUI.enabled = false;

            float max = Mathf.Max(20f, orig * 4f);
            GUI.Label(new Rect(r.x, r.y, 150f, RowH), Loc.T("slider.chain"));
            float nv = DoSlider("chain", new Rect(r.x + 155f, r.y + 8f, 120f, 20f), have ? len : 0.5f, 0.5f, max);
            // The value column doubles as the "no chain" hint, so the "Host only" tag at the right stays short and never gets cut off.
            GUI.Label(new Rect(r.x + 282f, r.y, 85f, RowH), have ? nv.ToString("F1") : Loc.T("label.nochain"));
            if (GUI.Button(new Rect(r.x + 370f, r.y, 70f, RowH - 2f), Loc.T("btn.reset"))) Controller.DoChain(2);
            GUI.enabled = wasEnabled;

            // Always visible, also when greyed out.
            GUI.Label(new Rect(r.x + 445f, r.y, r.width - 445f, RowH), Loc.T("label.hostonly"));

            if (host && have && Mathf.Abs(nv - len) > 0.0001f)
            {
                Actions.SetChainLength(nv, false);
                _chainDragging = true;
            }
        }

        private static void ButtonPair(string keyA, int idA, string keyB, int idB)
        {
            var r = Row(RowH + 6f);
            float half = (r.width - 8f) / 2f;
            if (GUI.Button(new Rect(r.x, r.y, half, RowH), Loc.T(keyA))) Controller.DoOnce(idA);
            if (GUI.Button(new Rect(r.x + half + 8f, r.y, half, RowH), Loc.T(keyB))) Controller.DoOnce(idB);
        }

        // ---------- slider (native when available, hand-made otherwise) ----------

        private static string _dragging;   // id of the fallback slider being dragged

        private static float DoSlider(string id, Rect r, float value, float min, float max)
        {
            if (_hasSlider) return GUI.HorizontalSlider(r, value, min, max);

            // Fallback: a track, a handle, and mouse handling through Event.current.
            float t = Mathf.InverseLerp(min, max, value);
            GUI.Box(new Rect(r.x, r.y + r.height / 2f - 2f, r.width, 4f), "");
            GUI.Box(new Rect(r.x + t * (r.width - 12f), r.y, 12f, r.height), "");
            var e = Event.current;
            if (e.type == EventType.MouseDown && r.Contains(e.mousePosition) && GUI.enabled) { _dragging = id; e.Use(); }
            if (e.type == EventType.MouseUp && _dragging == id) { _dragging = null; e.Use(); }
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && _dragging == id)
            {
                float nt = Mathf.Clamp01((e.mousePosition.x - r.x - 6f) / (r.width - 12f));
                value = Mathf.Lerp(min, max, nt);
                e.Use();
            }
            return value;
        }

        // "Numpad1" -> "Num1", "NumpadPlus" -> "Num+" ...
        private static string KeyLabel(string key)
        {
            return key.Replace("NumpadPlus", "Num+").Replace("NumpadMinus", "Num-")
                      .Replace("NumpadMultiply", "Num*").Replace("Numpad", "Num");
        }
    }
}
