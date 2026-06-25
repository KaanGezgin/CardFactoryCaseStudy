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

        float endDist;
        const float Spacing = 0.78f;
        const float CardLift = 0.8f;    // dik kart belt yüzeyinin üstünde dursun

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

        public bool TrySend(List<CardColor> group)
        {
            if (belt.Count + group.Count > cfg.beltMaxCards)
            {
                gate.FlashWarning();
                return false;
            }

            foreach (var color in group)
                SpawnCard(color);

            gate.SetCount(belt.Count, cfg.beltMaxCards);
            Sfx.Play("send");
            return true;
        }

        Vector3 WorldAt(float dist) => path.PointAt(dist) + Vector3.up * CardLift;

        void SpawnCard(CardColor color)
        {
            var go = Feedback.ProcMesh.RoundedCube("Card_" + color);
            go.transform.SetParent(transform, true);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.localScale = BeltCardScale;

            var card = go.AddComponent<Card>();
            card.Setup(color, MatFor(color));
            card.State = CardState.Belt;

            float backDist = 0f;
            foreach (var c in belt)
                if (c.BeltDist < backDist) backDist = c.BeltDist;
            card.BeltDist = belt.Count == 0 ? 0f : backDist - Spacing;

            go.transform.position = WorldAt(card.BeltDist);
            belt.Add(card);

            Juice.PopIn(go.transform, BeltCardScale, 0.15f);
        }

        void Update()
        {
            if (gm == null || gm.State != GameState.Playing || belt.Count == 0)
                return;

            belt.Sort((a, b) => b.BeltDist.CompareTo(a.BeltDist));

            for (int i = 0; i < belt.Count; i++)
            {
                var card = belt[i];
                float maxAllowed = (i == 0) ? endDist
                                            : Mathf.Min(endDist, belt[i - 1].BeltDist - Spacing);
                float target = Mathf.Min(endDist, maxAllowed);
                card.BeltDist = Mathf.MoveTowards(card.BeltDist, target, cfg.conveyorSpeed * Time.deltaTime);
                card.transform.position = WorldAt(card.BeltDist);
            }

            for (int i = belt.Count - 1; i >= 0; i--)
            {
                var card = belt[i];
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
