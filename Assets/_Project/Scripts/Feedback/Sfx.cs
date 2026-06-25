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
                { "click",    Tone(900f, 0.08f, 0.55f) },
                { "send",     Sweep(520f, 1000f, 0.16f, 0.5f) },
                { "fill",     Tone(720f, 0.07f, 0.42f) },
                { "ship",     Arp(new[] { 660f, 990f, 1320f }, 0.085f, 0.8f) },   // ka-ching
                { "complete", Arp(new[] { 523f, 659f, 784f, 1046f, 1318f }, 0.10f, 0.95f) }, // fanfar
                { "warn",     Tone(190f, 0.18f, 0.7f) },
                { "fail",     Arp(new[] { 392f, 311f, 233f, 165f }, 0.16f, 0.9f) }, // ağır iniş
            };
        }

        public static void Play(string name)
        {
            if (Application.isBatchMode) return;
            var s = Src;
            if (clips.TryGetValue(name, out var c) && c != null)
                s.PlayOneShot(c, 1f);
        }

        public static void Haptic()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (!Application.isEditor) Handheld.Vibrate();
#endif
        }

        // --- Sentez (harmonik zengin + atak/iniş zarfı → daha çarpıcı) ---

        /// <summary>Temel + 2./3. harmonik (normalize ~1 tepe) → daha dolgun ton.</summary>
        static float Voice(float phase)
        {
            return (Mathf.Sin(phase) + 0.45f * Mathf.Sin(2f * phase) + 0.22f * Mathf.Sin(3f * phase)) / 1.67f;
        }

        /// <summary>Hızlı atak + pürüzsüz iniş zarfı.</summary>
        static float Env(int i, int n)
        {
            float u = (float)i / n;
            float a = u < 0.02f ? u / 0.02f : 1f;          // ~2% atak (klik/pop hissi)
            return a * Mathf.Pow(1f - u, 1.3f);
        }

        static AudioClip Tone(float freq, float dur, float vol)
        {
            int n = Mathf.Max(1, (int)(Rate * dur));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float ph = 2f * Mathf.PI * freq * ((float)i / Rate);
                data[i] = Voice(ph) * vol * Env(i, n);
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
                float f = Mathf.Lerp(f0, f1, (float)i / n);
                phase += 2f * Mathf.PI * f / Rate;
                data[i] = Voice(phase) * vol * Env(i, n);
            }
            return MakeClip(data);
        }

        /// <summary>Arpej: her nota kendi pluck zarfıyla → ka-ching / fanfar / iniş.</summary>
        static AudioClip Arp(float[] freqs, float noteDur, float vol)
        {
            int seg = Mathf.Max(1, (int)(Rate * noteDur));
            int n = seg * freqs.Length;
            var data = new float[n];
            for (int k = 0; k < freqs.Length; k++)
                for (int j = 0; j < seg; j++)
                {
                    float ph = 2f * Mathf.PI * freqs[k] * ((float)j / Rate);
                    data[k * seg + j] = Voice(ph) * vol * Env(j, seg);
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
