using System.Collections;
using System.Collections.Generic;
using CardFactory.Data;
using CardFactory.Feedback;
using CardFactory.Gameplay;
using CardFactory.UI;
using UnityEngine;

namespace CardFactory.Core
{
    /// <summary>
    /// Otomatik REKLAM yönetmeni (Part 3). Benchmark'a (Match Factory! · Ad Type 3) göre TEK
    /// kreatifte: fail-bait (savruk oyun → DOCK DOLAR → gerçek FAILED) → success (doğru oyun →
    /// TÜM KARTLAR biter → gerçek COMPLETE) → CTA ("Play now"). Renk psikolojisi: kırmızı/yeşil.
    ///
    /// Sahneyi KENDİSİ oynar: el imlecini hedef desteye sürer, `CardStack.OnTapped()` çağırır.
    /// Beat panelleri için `HudController`'ın yuvarlatılmış UI'sını kullanır. Kalıcı; 'A' tuşuyla
    /// (veya `GameConfig.adMode`) başlar. Demo board deterministik (`DefaultLevels.GetDemo`).
    /// </summary>
    public class AdDirector : MonoBehaviour
    {
        public KeyCode startKey = KeyCode.A;
        public float   startDelay = 3f;   // A'ya basınca reklam bu kadar sn SONRA başlar (kaydı hazırlamak için pay)

        bool running;

        static readonly Color FailFront = new Color(0.93f, 0.27f, 0.30f);
        static readonly Color FailTint = new Color(0.45f, 0.05f, 0.06f, 0.34f);
        static readonly Color WinFront = new Color(0.25f, 0.72f, 0.35f);
        static readonly Color WinTint = new Color(0.06f, 0.40f, 0.16f, 0.30f);

        void Start()
        {
            if (GameBootstrap.AdMode) StartAd();
        }

        void Update()
        {
            if (!running && Input.GetKeyDown(startKey)) StartAd();
        }

        public void StartAd()
        {
            if (running) return;
            running = true;
            StartCoroutine(RunAd());
        }

        IEnumerator RunAd()
        {
            // A'ya basınca reklam HEMEN başlamaz → Recorder'ı başlatıp pencereye dönmek için pay.
            if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

            GameBootstrap.AdMode = true;
            GameBootstrap.AdWinnableBoard = false;   // 1. aşama: kazanılamaz fail board
            GameBootstrap.RequestRebuild(0);
            yield return Sequence();
        }

        // ---------------------------------------------------------------------
        //  SEKANS
        // ---------------------------------------------------------------------

        IEnumerator Sequence()
        {
            yield return WaitForBoard();
            Hud()?.HideAdMessage();
            SetupHand();

            // Beat 0 — Hook.
            yield return new WaitForSeconds(1.3f);

            // Beat 1 — Fail-bait: kazanılamaz board → dock dolar → gerçek LEVEL FAILED.
            yield return PlayCareless(35);
            yield return new WaitForSeconds(0.3f);
            // Teklif widget'ı: "+4 slots / 400" → "New Dock / 1000".
            Object.FindFirstObjectByType<Dock>()?.ShowFailOffer();
            // Ses GameManager.OnDockFull'da çalıyor (tek sefer). Glow kapalı (beğenilmedi); X açık.
            Hud()?.ShowAdMessage("LEVEL FAILED", "So close!", FailFront, FailTint,
                showClose: true);
            yield return new WaitForSeconds(1.9f);
            Hud()?.HideAdMessage();

            // Reset — kazanılabilir (success) board.
            GameBootstrap.AdWinnableBoard = true;
            GameBootstrap.RequestRebuild(0);
            yield return WaitForBoard();
            SetupHand();
            yield return new WaitForSeconds(0.6f);

            // Beat 2 — Success: doğru oyna → tüm kartlar biter (gerçek COMPLETE).
            yield return PlaySmart(40);
            yield return new WaitForSeconds(0.4f);
            // Ses GameManager.Win'de çalıyor (tek sefer).
            Hud()?.ShowAdMessage("LEVEL COMPLETE", "Your turn — tap to play!", WinFront, WinTint);
            yield return new WaitForSeconds(2.0f);

            // Beat 3 — CTA (ekranda kalır).
            Hud()?.ShowAdMessage("Play now!", "It's free!", WinFront, WinTint);

            running = false;
        }

        /// <summary>Fail oyun: aktif-OLMAYAN tepe grubunu gönder → dock'a gider → dolunca fail.</summary>
        IEnumerator PlayCareless(int maxTaps)
        {
            bool shownBlocked = false;
            for (int i = 0; i < maxTaps; i++)
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.State == GameState.Lost) yield break;   // dock doldu

                // Bant DOLUYKEN bilerek 1-2 kez dene → "dolu, koyamıyor" görünsün (kırmızı flaş).
                if (!shownBlocked && BeltCount() >= 19)
                {
                    var fs = FindWrongTopStack() ?? AnyNonEmptyStack();
                    for (int k = 0; k < 2 && fs != null && !fs.IsEmpty; k++)
                        yield return TapStack(fs, 0.8f);   // belt dolu → TrySend engeller → uyarı flaşı
                    shownBlocked = true;
                }

                var s = FindWrongTopStack() ?? AnyNonEmptyStack();
                if (s == null) break;
                yield return TapStack(s, 0.32f);
            }
            yield return WaitForLost(8f);   // gönderilen kartlar dock'a varıp doldursun
        }

        static int BeltCount()
        {
            var c = Object.FindFirstObjectByType<Conveyor>();
            return c != null ? c.BeltCount : 0;
        }

        IEnumerator WaitForLost(float timeout)
        {
            float t = 0f;
            while (t < timeout)
            {
                if (GameManager.Instance != null && GameManager.Instance.State == GameState.Lost) yield break;
                t += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>Akıllı oyun: tepesi AKTİF renk olan desteyi gönder → kutuya iner → kazanır.</summary>
        IEnumerator PlaySmart(int maxTaps)
        {
            for (int i = 0; i < maxTaps; i++)
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.State == GameState.Won) yield break;
                if (AllStacksEmpty()) break;

                var s = FindActiveTopStack();
                if (s == null) { yield return new WaitForSeconds(0.4f); continue; }   // akış işlensin
                yield return TapStack(s, 0.85f);
            }
            yield return WaitForWin(6f);   // banttaki son kartlar insin → COMPLETE
        }

        IEnumerator TapStack(CardStack s, float settle)
        {
            var hand = FindHand();
            if (hand != null) hand.SetAutoTarget(TapPoint(s));
            yield return new WaitForSeconds(0.45f);          // el süzülür (boşta hafif sürüklenir)
            if (hand != null) hand.Press();
            yield return new WaitForSeconds(0.12f);
            if (s != null && !s.IsEmpty) s.OnTapped();
            yield return new WaitForSeconds(settle);
        }

        static Vector3 TapPoint(CardStack s) => s.transform.position + Vector3.up * 0.4f;

        // ---------------------------------------------------------------------
        //  BEKLEMELER / SAHNE SORGULARI (rebuild sonrası taze objeler)
        // ---------------------------------------------------------------------

        IEnumerator WaitForBoard()
        {
            float t = 0f;
            while (t < 3f)
            {
                if (GameManager.Instance != null &&
                    GameManager.Instance.State == GameState.Playing &&
                    GetStacks().Count > 0)
                    yield break;
                t += Time.deltaTime;
                yield return null;
            }
        }

        IEnumerator WaitForWin(float timeout)
        {
            float t = 0f;
            while (t < timeout)
            {
                if (GameManager.Instance != null && GameManager.Instance.State == GameState.Won) yield break;
                t += Time.deltaTime;
                yield return null;
            }
        }

        void SetupHand()
        {
            var hand = FindHand();
            var stacks = GetStacks();
            if (hand != null && stacks.Count > 0) hand.SetAutoTarget(TapPoint(stacks[0]));
        }

        static HudController Hud() => Object.FindFirstObjectByType<HudController>();
        static HandPointer FindHand() => Object.FindFirstObjectByType<HandPointer>();

        static List<CardStack> GetStacks()
        {
            return new List<CardStack>(Object.FindObjectsByType<CardStack>(FindObjectsSortMode.None));
        }

        static bool AllStacksEmpty()
        {
            foreach (var s in GetStacks()) if (s != null && !s.IsEmpty) return false;
            return true;
        }

        static CardStack AnyNonEmptyStack()
        {
            foreach (var s in GetStacks()) if (s != null && !s.IsEmpty) return s;
            return null;
        }

        static HashSet<CardColor> ActiveColors()
        {
            var set = new HashSet<CardColor>();
            var bm = Object.FindFirstObjectByType<BinManager>();
            if (bm != null && bm.Slots != null)
                foreach (var b in bm.Slots)
                    if (b != null && b.Active) set.Add(b.Color);
            return set;
        }

        CardStack FindActiveTopStack()
        {
            var active = ActiveColors();
            foreach (var s in GetStacks())
                if (s != null && !s.IsEmpty && s.TopColor.HasValue && active.Contains(s.TopColor.Value))
                    return s;
            return null;
        }

        CardStack FindWrongTopStack()
        {
            var active = ActiveColors();
            foreach (var s in GetStacks())
                if (s != null && !s.IsEmpty && s.TopColor.HasValue && !active.Contains(s.TopColor.Value))
                    return s;
            return null;
        }
    }
}
