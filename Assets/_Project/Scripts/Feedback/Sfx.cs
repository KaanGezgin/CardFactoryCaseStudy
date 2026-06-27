using System.Collections.Generic;
using UnityEngine;

namespace CardFactory.Feedback
{
    /// <summary>
    /// Procedural SFX: NO audio files; clips are synthesized in code. Light haptics on
    /// mobile. The AudioSource is kept on a persistent object.
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
                { "click",    Tone(500f,  0.09f, 0.82f, hit:true) },                                   // select: punchy mid hit
                { "send",     Sweep(340f, 680f,  0.15f, 0.76f, hit:true) },                            // send: upward swoosh
                { "fill",     Arp(new[] { 420f, 630f },             0.07f, 0.74f, hit:true) },         // slotting into a bin: double pluck
                { "dock",     Sweep(400f, 190f,  0.18f, 0.78f, hit:true) },                            // dock drop: deep plop
                { "ship",     Arp(new[] { 330f, 440f, 660f },       0.10f, 0.82f) },                   // ship: warm ka-ching
                { "complete", ArpRing(new[] { 392f, 523f, 659f, 784f, 1046f }, 0.10f, 0.45f, 0.55f) }, // fanfare: lively, ringing, bright (less thick)
                { "warn",     Sweep(260f, 200f,  0.20f, 0.78f, hit:true) },                            // warning: low fall
                { "fail",     ArpRing(new[] { 587f, 440f, 349f, 262f }, 0.18f, 0.85f, 0.65f) },        // fail: lively ringing descent (light, less thick) — long ring
                { "tick",     Tone(780f,  0.028f, 0.55f, hit:true) },                                  // card entered the belt: soft tick
            };
        }

        const float Master = 1.0f;   // overall volume

        public static void Play(string name) => Play(name, 1f);

        /// <summary>volScale: one-shot volume scale (e.g. 0.4 for a very soft "tick").</summary>
        public static void Play(string name, float volScale)
        {
            if (Application.isBatchMode) return;
            var s = Src;
            if (clips.TryGetValue(name, out var c) && c != null)
                s.PlayOneShot(c, Master * volScale);
        }

        public static void Haptic()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (!Application.isEditor) Handheld.Vibrate();
#endif
        }

        // --- Synthesis ---

        /// <summary>Fundamental + octave body + 5th presence → warm, thick, not shrill.</summary>
        static float Voice(float phase)
        {
            return Mathf.Sin(phase) * 0.65f
                 + Mathf.Sin(2f * phase) * 0.28f
                 + Mathf.Sin(3f * phase) * 0.07f;
        }

        /// <summary>Melodic: soft attack, long sustain → for ship/complete/fail.</summary>
        static float Env(int i, int n)
        {
            float u = (float)i / n;
            float a = u < 0.04f ? u / 0.04f : 1f;
            return a * Mathf.Pow(1f - u, 1.1f);
        }

        /// <summary>Percussive: 1 ms anti-click fade + fast exp decay → punchy hit feel.</summary>
        static float EnvHit(int i, int n)
        {
            float u = (float)i / n;
            float a = i < 44 ? (float)i / 44f : 1f;
            return a * Mathf.Exp(-7f * u);
        }

        static AudioClip Tone(float freq, float dur, float vol, bool hit = false)
        {
            int n = Mathf.Max(1, (int)(Rate * dur));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float ph = 2f * Mathf.PI * freq * ((float)i / Rate);
                data[i] = Voice(ph) * vol * (hit ? EnvHit(i, n) : Env(i, n));
            }
            return MakeClip(data);
        }

        static AudioClip Sweep(float f0, float f1, float dur, float vol, bool hit = false)
        {
            int n = Mathf.Max(1, (int)(Rate * dur));
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float f = Mathf.Lerp(f0, f1, (float)i / n);
                phase += 2f * Mathf.PI * f / Rate;
                data[i] = Voice(phase) * vol * (hit ? EnvHit(i, n) : Env(i, n));
            }
            return MakeClip(data);
        }

        /// <summary>Arpeggio: each note with its own envelope → ka-ching / fanfare / descent.</summary>
        static AudioClip Arp(float[] freqs, float noteDur, float vol, bool hit = false)
        {
            int seg = Mathf.Max(1, (int)(Rate * noteDur));
            int n = seg * freqs.Length;
            var data = new float[n];
            for (int k = 0; k < freqs.Length; k++)
                for (int j = 0; j < seg; j++)
                {
                    float ph = 2f * Mathf.PI * freqs[k] * ((float)j / Rate);
                    data[k * seg + j] = Voice(ph) * vol * (hit ? EnvHit(j, seg) : Env(j, seg));
                }
            return MakeClip(data);
        }

        /// <summary>Light/bright tone: fundamental-heavy + small upper sparkle, low body → LESS THICK.</summary>
        static float BrightVoice(float phase)
        {
            return Mathf.Sin(phase) * 0.80f
                 + Mathf.Sin(2f * phase) * 0.14f
                 + Mathf.Sin(4f * phase) * 0.06f;   // 2 octaves up sparkle = brightness (low body)
        }

        /// <summary>
        /// Ringing arpeggio: notes overlap (legato, step &lt; ring) + long soft decay
        /// → LIVELY, LONG, less thick. For complete/fail. Blended with a soft-clip.
        /// </summary>
        static AudioClip ArpRing(float[] freqs, float step, float ring, float vol)
        {
            int seg  = Mathf.Max(1, (int)(Rate * step));   // spacing between notes
            int tail = Mathf.Max(1, (int)(Rate * ring));   // ring tail of each note
            int n    = seg * (freqs.Length - 1) + tail;
            var data = new float[n];
            for (int k = 0; k < freqs.Length; k++)
            {
                int start = k * seg;
                for (int j = 0; j < tail && start + j < n; j++)
                {
                    float u   = (float)j / tail;
                    float a   = j < 220 ? (float)j / 220f : 1f;     // ~5 ms soft attack
                    float env = a * Mathf.Exp(-3.2f * u);           // long, soft decay (rings)
                    float ph  = 2f * Mathf.PI * freqs[k] * ((float)j / Rate);
                    data[start + j] += BrightVoice(ph) * vol * env;
                }
            }
            for (int i = 0; i < n; i++) data[i] = data[i] / (1f + Mathf.Abs(data[i]));   // soft-clip (overlap must not clip)
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
