using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CBMod
{
    // All feature logic lives here, in a plain static class. Hotkeys (Update) and the menu (Hud) both call
    // the same Do* methods, so they share one state, one set of sounds, messages and log lines.
    // ModBehaviour (the object the game actually runs) only forwards Unity callbacks to this class.
    internal static class Controller
    {
        private static string _msg = "";
        private static float _msgUntil;
        private static Beeper _beep;
        private static float _nextMoneyTick;
        private static int _lastLoggedMoney = -1;

        public static void Start() { _beep = new Beeper(); }

        // ---------- Unity callbacks ----------

        public static void Update()
        {
            TickMoney();
            if (Hud.Open) Hud.KeepCursorFree();

            var kb = Keyboard.current;
            if (kb == null || _beep == null) return;

            if (Pressed(kb, Cfg.Menu)) Hud.Toggle();
            if (Hud.Open && kb.escapeKey.wasPressedThisFrame) Hud.Toggle();

            if (Pressed(kb, Cfg.GodMode)) DoGod();
            if (Pressed(kb, Cfg.Stamina)) DoStamina();
            if (Pressed(kb, Cfg.Injuries)) DoOnce(0);
            if (Pressed(kb, Cfg.Characters)) DoOnce(1);
            if (Pressed(kb, Cfg.AllData)) DoOnce(2);
            if (Pressed(kb, Cfg.Money)) DoMoney();
            if (Pressed(kb, Cfg.OneHit)) DoOneHit();
            if (Pressed(kb, Cfg.CameraZoom)) DoZoom();
            if (Pressed(kb, Cfg.Cosmetics)) DoOnce(3);

            if (Pressed(kb, Cfg.ChainUp)) DoChain(0);
            if (Pressed(kb, Cfg.ChainDown)) DoChain(1);
            if (Pressed(kb, Cfg.ChainReset)) DoChain(2);
        }

        public static void LateUpdate()
        {
            if (Hud.Open) Hud.KeepCursorFree();   // the game may re-lock the cursor in its own Update
            // After the game's own camera code, so our zoom is applied last.
            var note = CameraZoom.Tick();
            if (note != null) Show(note);
        }

        public static void OnGui()
        {
            // If we stopped being the host (left the lobby etc.), switch host-only features off quietly.
            if (!Features.IsHost()) { Features.GodMode = false; Features.InfiniteMoney = false; }

            Fonts.EnsureInitialised();
            Hud.ProbeOnce();
            Hud.Draw();
            DrawNotification();
        }

        // ---------- helpers ----------

        // True if the key named in the config (e.g. "Numpad1") was pressed this frame.
        private static bool Pressed(Keyboard kb, ConfigEntry<string> entry)
        {
            if (!Enum.TryParse<Key>(entry.Value, true, out var key)) return false;
            return kb[key].wasPressedThisFrame;
        }

        public static void Show(string text)
        {
            _msg = text;
            _msgUntil = Time.unscaledTime + 2.5f;
        }

        private static void DrawNotification()
        {
            if (Time.unscaledTime > _msgUntil) return;
            var style = new GUIStyle { fontSize = 20, fontStyle = FontStyle.Bold };
            style.normal.textColor = Color.white;
            if (Fonts.Active != null) style.font = Fonts.Active;
            GUI.Label(new Rect(20, 20, 900, 40), _msg, style);
        }

        // ---------- feature actions (hotkeys and menu both call these) ----------

        public static void DoGod()
        {
            if (!Features.IsHost())
            {
                Features.GodMode = false;
                Refuse("msg.god_host_only", "God mode refused: not the host.");
                return;
            }
            Toggle("feat.god", "God mode", ref Features.GodMode);
        }

        public static void DoStamina() { Toggle("feat.stamina", "Infinite stamina", ref Features.InfiniteStamina); }

        public static void DoOneHit() { Toggle("feat.onehit", "One-hit kill", ref Features.OneHitKill); }

        public static void DoZoom()
        {
            Toggle("feat.zoom", "Camera zoom out", ref Features.CameraZoomOut);
            if (Features.CameraZoomOut) CameraZoom.Dump();   // list all cameras once, for troubleshooting
        }

        public static void DoMoney()
        {
            // Run money is shared by the whole group, so only the host may change it.
            if (!Features.IsHost())
            {
                Features.InfiniteMoney = false;
                Refuse("msg.money_host_only", "Infinite money refused: not the host.");
                return;
            }
            Toggle("feat.money", "Infinite money", ref Features.InfiniteMoney);
            _nextMoneyTick = 0f;
            _lastLoggedMoney = -1;
            if (Features.InfiniteMoney) Plugin.L.LogInfo($"Infinite money target amount = {Cfg.MoneyAmount.Value}");
        }

        // 0 = longer, 1 = shorter, 2 = reset. Host only: the chain ties every player together.
        public static void DoChain(int what)
        {
            if (!Features.IsHost())
            {
                Refuse("msg.chain_host_only", "Chain length change refused: not the host.");
                return;
            }
            Actions.NoteKey = null;
            bool ok = false;
            try
            {
                float step = Cfg.ChainLengthStep.Value;
                ok = what == 0 ? Actions.ChangeChainLength(step)
                   : what == 1 ? Actions.ChangeChainLength(-step)
                   : Actions.ResetChainLength();
            }
            catch (Exception e) { Plugin.L.LogError(e.ToString()); }
            string okKey = Actions.NoteKey ?? (what == 0 ? "msg.chain_up" : what == 1 ? "msg.chain_down" : "msg.chain_reset");
            Show(Loc.T(ok ? okKey : "msg.chain_none"));
            if (ok) _beep.Confirm(); else _beep.Deny();
        }

        // 0 = clear injuries, 1 = characters, 2 = all data, 3 = cosmetics.
        public static void DoOnce(int id)
        {
            string okKey = id == 0 ? "msg.injuries_ok" : id == 1 ? "msg.chars_ok" : id == 2 ? "msg.alldata_ok" : "msg.cosm_ok";
            string failKey = id == 0 ? "msg.injuries_fail" : id == 3 ? "msg.cosm_fail" : "msg.unlock_fail";
            bool ok = false;
            Actions.NoteKey = null;
            try
            {
                ok = id switch
                {
                    0 => Actions.ClearInjuries(),
                    1 => Actions.UnlockCharacters(),
                    2 => Actions.UnlockAllData(),
                    _ => Actions.UnlockCosmetics(),
                };
            }
            catch (Exception e) { Plugin.L.LogError(e.ToString()); }
            Show(Loc.T(ok ? (Actions.NoteKey ?? okKey) : failKey));
            if (ok) _beep.Confirm(); else _beep.Deny();
        }

        // Keeps run money topped up while infinite money is on.
        private static void TickMoney()
        {
            if (!Features.InfiniteMoney) return;
            if (Time.unscaledTime < _nextMoneyTick) return;
            _nextMoneyTick = Time.unscaledTime + 0.5f;
            if (!Features.IsHost()) { Features.InfiniteMoney = false; return; }
            try
            {
                var rdm = UnityEngine.Object.FindObjectOfType<RunDataManager>();
                if (rdm == null) return;
                int target = Cfg.MoneyAmount.Value;
                int before = rdm.Money;
                if (before < target)
                {
                    rdm.ModifyMoney(target - before);
                    // Log only when the amount actually changed since the last log line (the shop spends money).
                    if (before != _lastLoggedMoney)
                        Plugin.L.LogInfo($"Infinite money: topped up {before} -> {rdm.Money} (target {target})");
                    _lastLoggedMoney = rdm.Money;
                }
            }
            catch (Exception e)
            {
                Features.InfiniteMoney = false;
                Plugin.L.LogError("Infinite money failed, switched off: " + e.Message);
            }
        }

        private static void Toggle(string nameKey, string logName, ref bool flag)
        {
            flag = !flag;
            Show(Loc.T(nameKey) + ": " + Loc.T(flag ? "state.on" : "state.off"));
            if (flag) _beep.On(); else _beep.Off();
            Plugin.L.LogInfo(logName + " = " + flag);
        }

        private static void Refuse(string msgKey, string logMsg)
        {
            Show(Loc.T(msgKey));
            Plugin.L.LogInfo(logMsg);
            _beep.Deny();
        }
    }
}
