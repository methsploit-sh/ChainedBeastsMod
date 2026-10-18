using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace CBMod
{
    // No sound files: sine waves are generated in code and turned into clips with AudioClip.Create.
    //
    // Earlier bug: clips were generated but were NULL by the time they were played, because Unity
    // unloads assets it considers unreferenced when scenes change. Two safeguards:
    //  1) hideFlags = HideAndDontSave  -> Unity does not unload the clip
    //  2) if the clip is gone anyway, it is regenerated right before playing
    internal class Beeper
    {
        private const int Rate = 44100;
        private AudioSource _src;

        private class Beep
        {
            public string Name;
            public (float hz, float sec)[] Tones;
            public AudioClip Clip;
        }

        private readonly Beep _on = new Beep { Name = "cb_on", Tones = new[] { (1300f, 0.09f) } };                 // high
        private readonly Beep _off = new Beep { Name = "cb_off", Tones = new[] { (520f, 0.12f) } };                // low
        private readonly Beep _ok = new Beep { Name = "cb_ok", Tones = new[] { (800f, 0.07f), (1100f, 0.09f) } };  // confirm
        private readonly Beep _deny = new Beep { Name = "cb_deny", Tones = new[] { (300f, 0.08f), (220f, 0.12f) } };

        public void On() => Play("on", _on);
        public void Off() => Play("off", _off);
        public void Confirm() => Play("ok", _ok);
        public void Deny() => Play("deny", _deny);

        // Finds the AudioListener (usually on the camera); the AudioSource is attached to its object.
        // The object is destroyed on scene changes, so this is re-checked on every play.
        private static AudioListener FindListener()
        {
            var l = UnityEngine.Object.FindObjectOfType<AudioListener>();
            if (l == null && Camera.main != null) l = Camera.main.GetComponent<AudioListener>();
            return l;
        }

        private bool EnsureSource(AudioListener l)
        {
            if (_src != null) return true;   // Unity null check: false if destroyed
            if (l == null) return false;
            _src = l.gameObject.AddComponent<AudioSource>();
            _src.spatialBlend = 0f;                  // 2D
            _src.playOnAwake = false;
            _src.loop = false;
            _src.mute = false;
            _src.priority = 0;                       // highest priority
            _src.ignoreListenerVolume = true;        // still audible if the game lowers AudioListener.volume
            _src.ignoreListenerPause = true;
            _src.bypassEffects = true;
            _src.bypassListenerEffects = true;
            _src.bypassReverbZones = true;
            Plugin.L.LogInfo("Beeper: AudioSource attached to " + l.gameObject.name);
            return true;
        }

        private void Play(string tag, Beep b)
        {
            try
            {
                if (b.Clip == null)   // never generated, or Unity unloaded it
                {
                    b.Clip = Make(b.Name, b.Tones);
                    Plugin.L.LogInfo($"Beeper[{tag}]: clip (re)generated");
                }
                var clip = b.Clip;
                if (clip == null) return;

                float vol = Mathf.Clamp01(Cfg.BeepVolume.Value);
                var l = FindListener();
                Plugin.L.LogInfo($"Beeper[{tag}]: cfgVol={Cfg.BeepVolume.Value} listenerVol={AudioListener.volume} " +
                                 $"listenerPause={AudioListener.pause} listener={(l != null ? l.gameObject.name : "NONE")}");

                string mode = (Cfg.PlayMode.Value ?? "Play").Trim();
                if (mode.Equals("AtPoint", StringComparison.OrdinalIgnoreCase))
                {
                    var pos = l != null ? l.transform.position : Vector3.zero;
                    AudioSource.PlayClipAtPoint(clip, pos, vol);
                    return;
                }

                if (!EnsureSource(l)) { Plugin.L.LogWarning($"Beeper[{tag}]: no AudioListener/AudioSource, cannot play."); return; }
                _src.volume = vol;

                if (mode.Equals("OneShot", StringComparison.OrdinalIgnoreCase))
                    _src.PlayOneShot(clip, vol);
                else
                {
                    _src.clip = clip;
                    _src.Play();
                }
                Plugin.L.LogInfo($"Beeper[{tag}]: {mode} isPlaying={_src.isPlaying}");
            }
            catch (Exception e)
            {
                Plugin.L.LogError("Beeper.Play error: " + e);
            }
        }

        private static AudioClip Make(string name, (float hz, float sec)[] tones)
        {
            try
            {
                int total = 0;
                foreach (var t in tones) total += (int)(t.sec * Rate);
                var data = new Il2CppStructArray<float>(total);
                int pos = 0;
                foreach (var t in tones)
                {
                    int n = (int)(t.sec * Rate);
                    for (int i = 0; i < n; i++)
                    {
                        // Short fade in/out at the edges prevents clicks.
                        float env = Mathf.Min(1f, Mathf.Min(i, n - i) / (Rate * 0.008f));
                        data[pos++] = Mathf.Sin(2f * Mathf.PI * t.hz * i / Rate) * env;
                    }
                }
                var clip = AudioClip.Create(name, total, 1, Rate, false);
                clip.hideFlags = HideFlags.HideAndDontSave;   // do not let Unity unload it on scene changes
                bool set = clip.SetData(data, 0);
                Plugin.L.LogInfo($"Beeper.Make[{name}]: samples={clip.samples} length={clip.length:F3}s SetData={set}");
                return clip;
            }
            catch (Exception e)
            {
                Plugin.L.LogError($"Beeper.Make[{name}] error: " + e);
                return null;
            }
        }
    }
}
