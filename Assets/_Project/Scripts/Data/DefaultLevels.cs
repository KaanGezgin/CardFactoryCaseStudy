using System.Collections.Generic;
using UnityEngine;

namespace CardFactory.Data
{
    /// <summary>
    /// Generates levels FROM CODE and RANDOMLY (no assets). For ad capture: readable, easy,
    /// winnable boards.
    ///
    /// CONSTRAINTS:
    ///  • 4 stacks, each Height(12) cards. Each color totals ColorTotal(12) cards = a multiple of
    ///    BinCap(6) → bins ALWAYS fill to a full 6 (no half bins; sending cards to the dock doesn't break bin logic).
    ///  • Colors come in BLOCKS: the same color RUNS 3..5 cards (MinRun..MaxRun). Each color appears
    ///    as a single block per stack → consecutive blocks are different colors.
    ///  • Color distribution is a "doubly-balanced" matrix: each cell ∈ {0,3,4,5}, row=col=12.
    ///
    /// WINNABILITY: Since bins ship only at a full 6, sending cards to the dock breaks the queue →
    /// the board must be ZERO-DOCK winnable. A MIXED block order is tried first (verified with
    /// `IsWinnable`); if none is found it falls back to the BANDED order (rank order, guaranteed zero-dock).
    /// </summary>
    public static class DefaultLevels
    {
        const int Colors = 4;
        const int Stacks = 4;
        const int Height = 12;        // stack height (4×12 = 48 cards)
        const int MinRun = 3;         // minimum same-color run
        const int MaxRun = 5;         // maximum same-color run
        const int BinCap = 6;
        const int DockCap = 20;
        const int BeltMax = 20;
        const int MaxAttempts = 120;

        static readonly CardColor[] Palette =
            { CardColor.Red, CardColor.Green, CardColor.Blue, CardColor.Yellow };

        public static int Count => 2;

        public static LevelData Get(int index) => Generate(new System.Random());

        /// <summary>
        /// AD FAIL board: stack TOPS are filled with NON-active colors (Blue/Red) → every tap goes
        /// to the dock → the dock DEFINITELY fills (unwinnable, "fail-bait"). The active colors
        /// (Green/Yellow) are buried at the bottom; the dock fills before they are reached. Order: Green→Yellow→Blue→Red.
        /// </summary>
        public static LevelData GetDemoFail()
        {
            var order = new List<CardColor>
            { CardColor.Green, CardColor.Yellow, CardColor.Blue, CardColor.Red };

            List<CardColor> Col(CardColor top, CardColor mid, CardColor bot)
            {
                var s = new List<CardColor>();
                for (int i = 0; i < 4; i++) s.Add(bot);
                for (int i = 0; i < 4; i++) s.Add(mid);
                for (int i = 0; i < 4; i++) s.Add(top);
                return s;
            }

            var stacks = new List<List<CardColor>>
            {
                Col(CardColor.Blue, CardColor.Red,  CardColor.Green),   // top Blue (not active)
                Col(CardColor.Red,  CardColor.Blue, CardColor.Yellow),  // top Red
                Col(CardColor.Blue, CardColor.Red,  CardColor.Green),
                Col(CardColor.Red,  CardColor.Blue, CardColor.Yellow),
            };

            return new LevelData
            {
                activeBinCount = 2,
                binCapacity = BinCap,
                dockCapacity = DockCap,
                beltMaxCards = BeltMax,
                binColorOrder = order,
                stacks = stacks,
            };
        }

        /// <summary>
        /// AD SUCCESS board. Order (rank): Green→Yellow→Blue→Red. BANDED (each stack top = earliest
        /// rank) → ZERO-DOCK winnable: with correct play all cards clear. Each color 12, blocks of 4.
        /// </summary>
        public static LevelData GetDemo()
        {
            var order = new List<CardColor>
            { CardColor.Green, CardColor.Yellow, CardColor.Blue, CardColor.Red };

            // top = top, bot = bottom. List index 0 = bottom.
            List<CardColor> Col(CardColor top, CardColor mid, CardColor bot)
            {
                var s = new List<CardColor>();
                for (int i = 0; i < 4; i++) s.Add(bot);
                for (int i = 0; i < 4; i++) s.Add(mid);
                for (int i = 0; i < 4; i++) s.Add(top);
                return s;
            }

            var stacks = new List<List<CardColor>>
            {
                Col(CardColor.Green,  CardColor.Yellow, CardColor.Blue),  // ranks 0,1,2
                Col(CardColor.Green,  CardColor.Yellow, CardColor.Red),   // ranks 0,1,3
                Col(CardColor.Green,  CardColor.Blue,   CardColor.Red),   // ranks 0,2,3
                Col(CardColor.Yellow, CardColor.Blue,   CardColor.Red),   // ranks 1,2,3
            };

            return new LevelData
            {
                activeBinCount = 2,
                binCapacity = BinCap,
                dockCapacity = DockCap,
                beltMaxCards = BeltMax,
                binColorOrder = order,
                stacks = stacks,
            };
        }

        static LevelData Generate(System.Random rng)
        {
            // Try a mixed block order → take it if zero-dock winnable.
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var matrix = BalancedMatrix(rng);
                var order = ShuffledOrder(rng);
                var level = BuildFromMatrix(matrix, order, rng, mixed: true);
                if (IsWinnable(level.stacks, order))
                    return level;
            }

            // Guaranteed: BANDED order (rank order, definite zero-dock).
            Debug.LogWarning("[DefaultLevels] No mixed zero-dock layout found; fell back to the banded layout.");
            return BuildFromMatrix(BalancedMatrix(rng), ShuffledOrder(rng), rng, mixed: false);
        }

        static List<CardColor> ShuffledOrder(System.Random rng)
        {
            var order = new List<CardColor>(Palette);
            Shuffle(order, rng);
            return order;
        }

        // ---------------------------------------------------------------------
        //  COLOR DISTRIBUTION: doubly-balanced matrix (cell ∈ {0,3,4,5}, row=col=12)
        // ---------------------------------------------------------------------

        /// <summary>
        /// matrix[s][r] = number of rank-r cards in stack s. Start: each color SKIPS one stack
        /// (a permutation), with 4 in each of the other 3 stacks → row=col=12, cells {0,4}. Then
        /// sum-preserving 2×2 swaps add 3/5 variety (cells {0,3,4,5}).
        /// </summary>
        static int[][] BalancedMatrix(System.Random rng)
        {
            var skip = new int[Colors];                 // skip[r] = the stack that rank r skips
            for (int r = 0; r < Colors; r++) skip[r] = r;
            Shuffle(skip, rng);                          // permutation → each stack skips exactly 1 color

            var m = new int[Stacks][];
            for (int s = 0; s < Stacks; s++)
            {
                m[s] = new int[Colors];
                for (int r = 0; r < Colors; r++) m[s][r] = (skip[r] == s) ? 0 : 4;
            }

            for (int k = 0; k < 24; k++)
            {
                int s1 = rng.Next(Stacks), s2 = rng.Next(Stacks);
                int c1 = rng.Next(Colors), c2 = rng.Next(Colors);
                if (s1 == s2 || c1 == c2) continue;
                // Preserve sums: s1c1--, s1c2++, s2c1++, s2c2--  (all must stay within {3,4,5}).
                if (m[s1][c1] >= 4 && m[s2][c2] >= 4 &&
                    m[s1][c2] >= 3 && m[s1][c2] <= 4 &&
                    m[s2][c1] >= 3 && m[s2][c1] <= 4)
                {
                    m[s1][c1]--; m[s2][c2]--;
                    m[s1][c2]++; m[s2][c1]++;
                }
            }
            return m;
        }

        static LevelData BuildFromMatrix(int[][] matrix, List<CardColor> order, System.Random rng, bool mixed)
        {
            var stacks = new List<List<CardColor>>(Stacks);
            for (int s = 0; s < Stacks; s++)
            {
                // Blocks: (rank, size) — size = cell value (>0).
                var blocks = new List<int>();            // list of ranks
                for (int r = 0; r < Colors; r++)
                    if (matrix[s][r] > 0) blocks.Add(r);
                if (mixed) Shuffle(blocks, rng);          // mixed order; otherwise rank-ascending (banded)

                var topDown = new List<CardColor>(Height);
                foreach (int r in blocks)
                    for (int k = 0; k < matrix[s][r]; k++) topDown.Add(order[r]);

                topDown.Reverse();                        // LevelData: index 0 = bottom
                stacks.Add(topDown);
            }

            return new LevelData
            {
                activeBinCount = 2,
                binCapacity = BinCap,
                dockCapacity = DockCap,
                beltMaxCards = BeltMax,
                binColorOrder = new List<CardColor>(order),
                stacks = stacks,
            };
        }

        // ---------------------------------------------------------------------
        //  ZERO-DOCK WINNABILITY SOLVER
        // ---------------------------------------------------------------------

        /// <summary>
        /// Can it be won without using the dock at all? Active = the first 2 colors in order that
        /// still have cards. A stack whose top is active is sent; if no top is active (and cards
        /// remain) → no.
        /// </summary>
        static bool IsWinnable(List<List<CardColor>> stacks, List<CardColor> order)
        {
            var st = new List<List<CardColor>>(stacks.Count);
            var remaining = new Dictionary<CardColor, int>();
            foreach (var s in stacks)
            {
                st.Add(new List<CardColor>(s));
                foreach (var c in s)
                {
                    remaining.TryGetValue(c, out int v);
                    remaining[c] = v + 1;
                }
            }

            int total = 0;
            foreach (var s in st) total += s.Count;

            for (int guard = 0; guard <= total + 5; guard++)
            {
                CardColor a0 = default, a1 = default;
                int activeCount = 0;
                foreach (var c in order)
                {
                    if (remaining.TryGetValue(c, out int r) && r > 0)
                    {
                        if (activeCount == 0) a0 = c; else a1 = c;
                        if (++activeCount == 2) break;
                    }
                }
                if (activeCount == 0) return true;

                bool moved = false;
                for (int i = 0; i < st.Count; i++)
                {
                    if (st[i].Count == 0) continue;
                    var top = st[i][st[i].Count - 1];
                    if (top != a0 && (activeCount < 2 || top != a1)) continue;

                    var t = top;
                    while (st[i].Count > 0 && st[i][st[i].Count - 1] == t)
                    { st[i].RemoveAt(st[i].Count - 1); remaining[t]--; }
                    moved = true;
                    break;
                }
                if (!moved) return false;
            }
            return false;
        }

        static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
