using System.Collections.Generic;
using CardFactory.Core;
using CardFactory.Data;
using UnityEngine;

namespace CardFactory.Gameplay
{
    /// <summary>
    /// Aktif kutuları (default 2) tutar; her renk binColorOrder sırasıyla dağıtılır,
    /// aktif 2 kutu hep farklı renk olur. Kutular bin ANCHOR'larının altına kurulur
    /// (anchor'lar kalıcı/taşınabilir; kutular her oyunda dinamik). Her kutunun
    /// kapasitesi o renkten KALAN kart kadardır → son kutu da tam dolar.
    /// </summary>
    public class BinManager : MonoBehaviour
    {
        GameConfig cfg;
        GameManager gm;
        int cap;

        List<CardColor> order;
        readonly Dictionary<CardColor, int> needed = new();      // renk başına gereken kutu
        readonly Dictionary<CardColor, int> shipped = new();     // sevk edilen kutu
        readonly Dictionary<CardColor, int> total = new();       // renk başına toplam kart
        readonly Dictionary<CardColor, int> captured = new();    // kutulara inen kart
        readonly Dictionary<CardColor, int> deployedCap = new(); // o renge dağıtılan toplam kapasite

        const float CaptureWindow = 1.5f;   // kutuyu bu kadar geçen kart artık geri dönmez

        public Bin[] Slots { get; private set; }

        public void Init(GameConfig config, LevelData level, GameManager gameManager,
                         Transform[] binAnchors, BeltPath path)
        {
            cfg = config;
            gm = gameManager;
            cap = level.binCapacity > 0 ? level.binCapacity : cfg.binCapacity;

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
                total[kv.Key] = kv.Value;
                captured[kv.Key] = 0;
                deployedCap[kv.Key] = 0;
            }

            Slots = new Bin[binAnchors.Length];
            for (int i = 0; i < binAnchors.Length; i++)
            {
                var anchor = binAnchors[i];
                var go = new GameObject($"Bin_{i}");
                go.transform.SetParent(anchor, false);
                var bin = go.AddComponent<Bin>();
                bin.Init(gm, this, i, anchor.position);
                bin.TriggerDist = path.NearestDist(anchor.position);
                Slots[i] = bin;
            }

            for (int i = 0; i < Slots.Length; i++)
                AssignSlot(i, OtherActiveColor(i));
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

        /// <summary>O renge bu kutu için kapasite = kalan kart (max base kapasite).</summary>
        int CapacityFor(CardColor c)
        {
            int remaining = total[c] - deployedCap[c];
            int thisCap = Mathf.Clamp(remaining, 1, cap);
            deployedCap[c] += thisCap;
            return thisCap;
        }

        void AssignSlot(int slot, CardColor? avoid)
        {
            var c = PickColor(avoid);
            if (c.HasValue) Slots[slot].Configure(c.Value, CapacityFor(c.Value));
            else Slots[slot].Deactivate();
        }

        public void NotifyCaptured(CardColor color)
        {
            if (captured.ContainsKey(color)) captured[color]++;
        }

        public bool IsColorExhausted(CardColor color)
        {
            return total.TryGetValue(color, out int t) &&
                   captured.TryGetValue(color, out int c) && c >= t;
        }

        public Bin FindCaptor(CardColor color, float dist)
        {
            Bin best = null;
            foreach (var bin in Slots)
            {
                if (bin == null || !bin.Active) continue;
                if (bin.Color != color || !bin.HasRoom) continue;
                if (dist < bin.TriggerDist - 0.4f) continue;          // henüz gelmedi
                if (dist > bin.TriggerDist + CaptureWindow) continue; // geçti → geri dönmez
                if (best == null || bin.TriggerDist < best.TriggerDist) best = bin;
            }
            return best;
        }

        public void OnBinShipped(int slot)
        {
            var c = Slots[slot].Color;
            shipped[c]++;

            CardColor? avoid = OtherActiveColor(slot);

            if (shipped[c] < needed[c] && (!avoid.HasValue || c != avoid.Value))
                Slots[slot].Configure(c, CapacityFor(c));   // aynı renk, kalan kart kadar
            else
                AssignSlot(slot, avoid);
        }
    }
}
