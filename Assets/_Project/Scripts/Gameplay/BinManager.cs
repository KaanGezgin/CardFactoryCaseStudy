using System.Collections.Generic;
using CardFactory.Core;
using CardFactory.Data;
using UnityEngine;

namespace CardFactory.Gameplay
{
    /// <summary>
    /// Aktif kutuları (default 2) tutar. Her renk binColorOrder sırasıyla
    /// dağıtılır; bir slot, rengi tükenene kadar AYNI rengi yeniler. Yeni renk
    /// seçerken diğer slottaki renk seçilmez → aktif 2 kutu HEP farklı renktir.
    /// </summary>
    public class BinManager : MonoBehaviour
    {
        GameConfig cfg;
        GameManager gm;
        int cap;

        List<CardColor> order;
        readonly Dictionary<CardColor, int> needed = new();   // renk başına gereken kutu
        readonly Dictionary<CardColor, int> shipped = new();  // sevk edilen kutu

        public Bin[] Slots { get; private set; }

        public void Init(GameConfig config, LevelData level, GameManager gameManager,
                         Vector3[] slotPositions, BeltPath path)
        {
            cfg = config;
            gm = gameManager;
            cap = level.binCapacity > 0 ? level.binCapacity : cfg.binCapacity;

            // Renk sayıları → renk başına gereken kutu adedi
            var counts = new Dictionary<CardColor, int>();
            foreach (var st in level.stacks)
                foreach (var c in st)
                {
                    counts.TryGetValue(c, out int v);
                    counts[c] = v + 1;
                }

            order = level.binColorOrder != null && level.binColorOrder.Count > 0
                ? new List<CardColor>(level.binColorOrder)
                : new List<CardColor>(counts.Keys);
            foreach (var c in counts.Keys)
                if (!order.Contains(c)) order.Add(c);

            foreach (var kv in counts)
            {
                needed[kv.Key] = Mathf.CeilToInt(kv.Value / (float)cap);
                shipped[kv.Key] = 0;
            }

            Slots = new Bin[slotPositions.Length];
            for (int i = 0; i < slotPositions.Length; i++)
            {
                var go = new GameObject($"Bin_{i}");
                go.transform.SetParent(transform, false);
                var bin = go.AddComponent<Bin>();
                bin.Init(gm, this, i, slotPositions[i]);
                bin.TriggerDist = path.NearestDist(slotPositions[i]);
                Slots[i] = bin;
            }

            // İlk dağıtım: her slot bir öncekinden farklı renk
            for (int i = 0; i < Slots.Length; i++)
            {
                CardColor? avoid = OtherActiveColor(i);
                AssignSlot(i, avoid);
            }
        }

        CardColor? OtherActiveColor(int slot)
        {
            for (int j = 0; j < Slots.Length; j++)
            {
                if (j == slot) continue;
                if (Slots[j] != null && Slots[j].Active) return Slots[j].Color;
            }
            return null;
        }

        CardColor? PickColor(CardColor? avoid)
        {
            foreach (var c in order)
            {
                if (!needed.ContainsKey(c)) continue;
                if (shipped[c] >= needed[c]) continue;
                if (avoid.HasValue && c == avoid.Value) continue;
                return c;
            }
            return null;
        }

        void AssignSlot(int slot, CardColor? avoid)
        {
            var c = PickColor(avoid);
            if (c.HasValue) Slots[slot].Configure(c.Value, cap);
            else Slots[slot].Deactivate();
        }

        public Bin FindCaptor(CardColor color, float dist)
        {
            Bin best = null;
            foreach (var bin in Slots)
            {
                if (bin == null || !bin.Active) continue;
                if (bin.Color != color || !bin.HasRoom) continue;
                if (dist < bin.TriggerDist - 0.4f) continue;
                if (best == null || bin.TriggerDist < best.TriggerDist) best = bin;
            }
            return best;
        }

        public void OnBinShipped(int slot)
        {
            var c = Slots[slot].Color;
            shipped[c]++;

            CardColor? avoid = OtherActiveColor(slot);

            // Renk tükenmediyse AYNI rengi yenile (diğer slot zaten farklı renk)
            if (shipped[c] < needed[c] && (!avoid.HasValue || c != avoid.Value))
                Slots[slot].Configure(c, cap);
            else
                AssignSlot(slot, avoid);
        }
    }
}
