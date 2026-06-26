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
                { "click",    Tone(500f,  0.09f, 0.82f, hit:true) },                                   // seçme: tok mid vuruş
                { "send",     Sweep(340f, 680f,  0.15f, 0.76f, hit:true) },                            // gönder: yukarı swoosh
                { "fill",     Arp(new[] { 420f, 630f },             0.07f, 0.74f, hit:true) },         // slota oturma: çift pluck
                { "dock",     Sweep(400f, 190f,  0.18f, 0.78f, hit:true) },                            // dock düşüş: derin plop
                { "ship",     Arp(new[] { 330f, 440f, 660f },       0.10f, 0.82f) },                   // sevk: sıcak ka-ching
                { "complete", ArpRing(new[] { 392f, 523f, 659f, 784f, 1046f }, 0.10f, 0.45f, 0.55f) }, // fanfar: canlı, çınlayan, parlak (az tok)
                { "warn",     Sweep(260f, 200f,  0.20f, 0.78f, hit:true) },                            // uyarı: alçak düşüş
                { "fail",     ArpRing(new[] { 587f, 440f, 349f, 262f }, 0.13f, 0.42f, 0.55f) },        // fail: canlı çınlayan iniş (hafif, az tok)
                { "tick",     Tone(780f,  0.028f, 0.55f, hit:true) },                                  // kart banda girdi: hafif tık
            };
        }

        const float Master = 1.0f;   // genel ses

        public static void Play(string name) => Play(name, 1f);

        /// <summary>volScale: tek seferlik ses ölçeği (örn. çok hafif "tık" için 0.4).</summary>
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

        // --- Sentez ---

        /// <summary>Fundamental + oktav body + 5th presence → sıcak, tok, tiz değil.</summary>
        static float Voice(float phase)
        {
            return Mathf.Sin(phase) * 0.65f
                 + Mathf.Sin(2f * phase) * 0.28f
                 + Mathf.Sin(3f * phase) * 0.07f;
        }

        /// <summary>Melodic: yumuşak atak, uzun sus → ship/complete/fail için.</summary>
        static float Env(int i, int n)
        {
            float u = (float)i / n;
            float a = u < 0.04f ? u / 0.04f : 1f;
            return a * Mathf.Pow(1f - u, 1.1f);
        }

        /// <summary>Percussive: 1 ms anti-click fade + hızlı exp decay → tok vuruş hissi.</summary>
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

        /// <summary>Arpej: her nota kendi zarfıyla → ka-ching / fanfar / iniş.</summary>
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

        /// <summary>Hafif/parlak ton: temel ağırlıklı + küçük üst kıvılcım, az gövde → AZ TOK.</summary>
        static float BrightVoice(float phase)
        {
            return Mathf.Sin(phase) * 0.80f
                 + Mathf.Sin(2f * phase) * 0.14f
                 + Mathf.Sin(4f * phase) * 0.06f;   // 2 oktav üstü kıvılcım = parlaklık (gövde düşük)
        }

        /// <summary>
        /// Çınlayan arpej: notalar üst üste biner (legato, step &lt; ring) + uzun yumuşak
        /// sönüm → CANLI, UZUN, az tok. complete/fail için. Yumuşak soft-clip ile birleşir.
        /// </summary>
        static AudioClip ArpRing(float[] freqs, float step, float ring, float vol)
        {
            int seg  = Mathf.Max(1, (int)(Rate * step));   // notalar arası aralık
            int tail = Mathf.Max(1, (int)(Rate * ring));   // her notanın çınlama kuyruğu
            int n    = seg * (freqs.Length - 1) + tail;
            var data = new float[n];
            for (int k = 0; k < freqs.Length; k++)
            {
                int start = k * seg;
                for (int j = 0; j < tail && start + j < n; j++)
                {
                    float u   = (float)j / tail;
                    float a   = j < 220 ? (float)j / 220f : 1f;     // ~5 ms yumuşak atak
                    float env = a * Mathf.Exp(-3.2f * u);           // uzun, yumuşak sönüm (çınlar)
                    float ph  = 2f * Mathf.PI * freqs[k] * ((float)j / Rate);
                    data[start + j] += BrightVoice(ph) * vol * env;
                }
            }
            for (int i = 0; i < n; i++) data[i] = data[i] / (1f + Mathf.Abs(data[i]));   // soft-clip (üst üste binme klip yapmasın)
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
