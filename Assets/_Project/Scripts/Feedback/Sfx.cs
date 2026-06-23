using System.Collections.Generic;
using UnityEngine;

namespace CardFactory.Feedback
{
    /// <summary>
    /// Prosedürel SFX: ses dosyası YOK; klipler koddan sentezlenir. Mobilde
    /// hafif haptik. AudioSource kalıcı bir objede tutulur.
    /// </summary>
    public static class Sfx
    {
        const int Rate = 44100;

        static AudioSource src;
        static Dictionary<string, AudioClip> clips;

        static AudioSource Src
        {
            get
            {
                if (src == null)
                {
                    var go = new GameObject("Sfx");
                    Object.DontDestroyOnLoad(go);
                    src = go.AddComponent<AudioSource>();
                    src.playOnAwake = false;
                    BuildClips();
                }
                return src;
            }
        }

        static void BuildClips()
        {
            clips = new Dictionary<string, AudioClip>
            {
                { "click",    Tone(880f, 0.07f, 0.30f) },
                { "send",     Sweep(500f, 900f, 0.14f, 0.28f) },
                { "fill",     Tone(660f, 0.05f, 0.18f) },
                { "ship",     TwoTone(660f, 988f, 0.22f, 0.40f) },
                { "complete", TwoTone(523f, 784f, 0.30f, 0.45f) },
                { "warn",     Tone(170f, 0.16f, 0.45f) },
                { "fail",     Sweep(400f, 150f, 0.5f, 0.45f) },
            };
        }

        public static void Play(string name)
        {
            if (Application.isBatchMode) return;
            var s = Src;
            if (clips.TryGetValue(name, out var c) && c != null)
                s.PlayOneShot(c);
        }

        public static void Haptic()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (!Application.isEditor) Handheld.Vibrate();
#endif
        }

        // --- Sentez ---

        static AudioClip Tone(float freq, float dur, float vol)
        {
            int n = Mathf.Max(1, (int)(Rate * dur));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Clamp01(1f - (float)i / n);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * vol * env;
            }
            return MakeClip(data);
        }

        static AudioClip Sweep(float f0, float f1, float dur, float vol)
        {
            int n = Mathf.Max(1, (int)(Rate * dur));
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n;
                float f = Mathf.Lerp(f0, f1, u);
                phase += 2f * Mathf.PI * f / Rate;
                float env = Mathf.Clamp01(1f - u);
                data[i] = Mathf.Sin(phase) * vol * env;
            }
            return MakeClip(data);
        }

        static AudioClip TwoTone(float f0, float f1, float dur, float vol)
        {
            int n = Mathf.Max(1, (int)(Rate * dur));
            var data = new float[n];
            int half = n / 2;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float f = i < half ? f0 : f1;
                float env = Mathf.Clamp01(1f - (float)i / n);
                data[i] = Mathf.Sin(2f * Mathf.PI * f * t) * vol * env;
            }
            return MakeClip(data);
        }

        static AudioClip MakeClip(float[] data)
        {
            var clip = AudioClip.Create("sfx", data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
