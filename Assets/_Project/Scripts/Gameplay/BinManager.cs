using System.Collections.Generic;
using CardFactory.Core;
using CardFactory.Data;
using UnityEngine;

namespace CardFactory.Gameplay
{
    /// <summary>
    /// Holds the active bins (default 2); colors are handed out in binColorOrder, and the
    /// 2 active bins are always different colors. Bins are built under the bin ANCHORS
    /// (anchors are persistent/movable; bins are dynamic per game). Each bin's capacity is
    /// the REMAINING cards of that color → the last bin also fills completely.
    /// </summary>
    public class BinManager : MonoBehaviour
    {
        GameConfig cfg;
        GameManager gm;
        int cap;

        List<CardColor> order;
        readonly Dictionary<CardColor, int> needed = new();      // bins needed per color
        readonly Dictionary<CardColor, int> shipped = new();     // bins shipped
        readonly Dictionary<CardColor, int> total = new();       // total cards per color
        readonly Dictionary<CardColor, int> captured = new();    // cards that landed in bins
        readonly Dictionary<CardColor, int> deployedCap = new(); // total capacity assigned to that color

        const float CaptureWindow = 1.5f;   // a card past the bin by this much no longer returns

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

        /// <summary>Capacity for this bin of that color = remaining cards (capped at base capacity).</summary>
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
                if (dist < bin.TriggerDist - 0.4f) continue;          // not there yet
                if (dist > bin.TriggerDist + CaptureWindow) continue; // passed → won't return
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
                Slots[slot].Configure(c, CapacityFor(c));   // same color, as many as remain
            else
                AssignSlot(slot, avoid);
        }
    }
}
