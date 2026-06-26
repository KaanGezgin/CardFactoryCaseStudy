using System.Collections;
using System.Collections.Generic;
using CardFactory.Core;
using CardFactory.Data;
using CardFactory.Feedback;
using UnityEngine;

namespace CardFactory.Gameplay
{
    /// <summary>
    /// Kartları U-şekilli yol boyunca taşır (BeltPath, yay-uzunluğu). Yol boyunca
    /// eşleşen kart bir kutuya çekilir; eşleşmeden yolun sonuna ulaşan Dock'a
    /// düşer. Belt sayacı (X/20) burada tutulur.
    /// </summary>
    public class Conveyor : MonoBehaviour
    {
        GameConfig cfg;
        GameManager gm;
        BinManager bins;
        Dock dock;
        FactoryGate gate;
        BeltPath path;
        Transform entryPortal;   // sahnedeki "GateSlot" → kartlar buradan giriyormuş gibi

        float endDist;
        const float Spacing = 0.78f;
        const float CardLift = 0.8f;    // dik kart belt yüzeyinin üstünde dursun
        const float BeltTurnSpeed = 540f;   // viraj takibi: kart ilerleme yönüne dönme hızı (°/s)

        // Bant kartı DİK durur (domino): X=genişlik, Y=yükseklik, Z=bant yönünde ince.
        static readonly Vector3 BeltCardScale = new Vector3(1f, 1.45f, 0.25f);

        readonly List<Card> belt = new();
        readonly Dictionary<CardColor, Material> cardMats = new();

        public int BeltCount => belt.Count;

        public void Init(GameConfig config, GameManager gameManager, BinManager binManager,
                         Dock dockRef, FactoryGate gateRef, BeltPath beltPath)
        {
            cfg = config;
            gm = gameManager;
            bins = binManager;
            dock = dockRef;
            gate = gateRef;
            path = beltPath;
            endDist = path.Length;
            gate.SetCount(0, cfg.beltMaxCards);

            // Giriş portalı = sahnedeki "GateSlot" objesi (kapının üst yarığı). Bulunamazsa
            // doğrudan belt başına girilir (eski davranış).
            entryPortal = gateRef != null && gateRef.transform.parent != null
                ? gateRef.transform.parent.Find("GateSlot")
                : null;
        }

        Material MatFor(CardColor color)
        {
            if (!cardMats.TryGetValue(color, out var m))
            {
                m = GameBootstrap.NewLitMaterial(CardPalette.Get(color));
                cardMats[color] = m;
            }
            return m;
        }

        public bool TrySend(List<CardColor> group, Vector3 origin)
        {
            if (belt.Count + group.Count > cfg.beltMaxCards)
            {
                gate.FlashWarning();
                return false;
            }

            for (int k = 0; k < group.Count; k++)
                SpawnCard(group[k], origin, k);

            gate.SetCount(belt.Count, cfg.beltMaxCards);
            Sfx.Play("send");
            return true;
        }

        Vector3 WorldAt(float dist) => path.PointAt(dist) + Vector3.up * CardLift;

        void SpawnCard(CardColor color, Vector3 origin, int index)
        {
            var go = Feedback.ProcMesh.RoundedCube("Card_" + color);
            go.transform.SetParent(transform, true);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.localScale = Vector3.zero;   // sırası gelene kadar gizli

            var card = go.AddComponent<Card>();
            card.Setup(color, MatFor(color));
            card.State = CardState.Belt;

            float backDist = 0f;
            foreach (var c in belt)
                if (c.BeltDist < backDist) backDist = c.BeltDist;
            card.BeltDist = belt.Count == 0 ? 0f : backDist - Spacing;

            go.transform.position = origin;
            card.Entering = true;
            // Sırayla (staggered) havaya kalkıp bant BAŞINDAN (kapı) içeri gir.
            StartCoroutine(EnterCard(card, WorldAt(0f), origin, index * 0.1f));
            belt.Add(card);
        }

        IEnumerator EnterCard(Card card, Vector3 beltEntry, Vector3 origin, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (card == null) yield break;

            card.transform.localScale = BeltCardScale;
            Juice.PopIn(card.transform, BeltCardScale, 0.12f);
            Sfx.Play("tick", 0.4f);   // kart süzülmeye başlar başlamaz → anında hafif tık (geç kalmasın)

            if (entryPortal != null)
            {
                // Aşama A: desteden GateSlot'a (üst yarık) yay çizerek girer.
                yield return ArcMove(card, origin, entryPortal.position, 1.1f, 0.34f);
                if (card == null) yield break;
                // Aşama B: yarıktan belt başına/ön ağza iner (makinenin içinden çıkmış gibi).
                yield return ArcMove(card, entryPortal.position, beltEntry, 0.15f, 0.24f);
            }
            else
            {
                // Portal yoksa: doğrudan belt başına yüksek yayla gir (eski davranış).
                yield return ArcMove(card, origin, beltEntry, 3.2f, 0.5f);
            }

            if (card != null)
            {
                card.transform.position = beltEntry;
                card.transform.rotation = Quaternion.LookRotation(path.TangentAt(0f), Vector3.up);  // giriş yönüyle başla
                card.Entering = false;
            }
        }

        /// <summary>Kartı from→to parabolik yay ile taşır (height = tepe yüksekliği).</summary>
        IEnumerator ArcMove(Card card, Vector3 from, Vector3 to, float height, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                if (card == null) yield break;
                float k = Mathf.Clamp01(t / dur);
                Vector3 p = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, k));
                p.y += height * 4f * k * (1f - k);
                card.transform.position = p;
                yield return null;
            }
            if (card != null) card.transform.position = to;
        }

        void Update()
        {
            if (gm == null || gm.State != GameState.Playing || belt.Count == 0)
                return;

            belt.Sort((a, b) => b.BeltDist.CompareTo(a.BeltDist));

            for (int i = 0; i < belt.Count; i++)
            {
                var card = belt[i];
                if (card.Entering) continue;   // stack'ten süzülüyor; konumunu MoveArc sürer
                float maxAllowed = (i == 0) ? endDist
                                            : Mathf.Min(endDist, belt[i - 1].BeltDist - Spacing);
                float target = Mathf.Min(endDist, maxAllowed);
                card.BeltDist = Mathf.MoveTowards(card.BeltDist, target, cfg.conveyorSpeed * Time.deltaTime);
                card.transform.position = WorldAt(card.BeltDist);

                // Viraj takibi: kartı yolun ilerleme yönüne (teğet) yumuşakça döndür.
                var want = Quaternion.LookRotation(path.TangentAt(card.BeltDist), Vector3.up);
                card.transform.rotation = Quaternion.RotateTowards(
                    card.transform.rotation, want, BeltTurnSpeed * Time.deltaTime);
            }

            for (int i = belt.Count - 1; i >= 0; i--)
            {
                var card = belt[i];
                if (card.Entering) continue;   // giriş bitene kadar yakalanmaz/dock'a düşmez
                var captor = bins.FindCaptor(card.Color, card.BeltDist);
                if (captor != null)
                {
                    belt.RemoveAt(i);
                    gate.SetCount(belt.Count, cfg.beltMaxCards);
                    captor.Accept(card);
                    continue;
                }

                if (card.BeltDist >= endDist - 0.001f)
                {
                    belt.RemoveAt(i);
                    gate.SetCount(belt.Count, cfg.beltMaxCards);
                    dock.Receive(card);
                }
            }
        }
    }
}
