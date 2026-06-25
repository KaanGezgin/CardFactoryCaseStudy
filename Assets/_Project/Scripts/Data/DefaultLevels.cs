using System.Collections.Generic;
using UnityEngine;

namespace CardFactory.Data
{
    /// <summary>
    /// Level'leri KODDAN ve RASTGELE üretir (asset yok). Reklam çekimi için: okunur, kolay,
    /// kazanılabilir board'lar.
    ///
    /// KISITLAR:
    ///  • 4 deste, her biri Height(12) kart. Her renk TOPLAM ColorTotal(12) kart = BinCap(6)'nın
    ///    katı → kutular DAİMA tam 6 dolar (yarım kutu yok; dock'a kart gitse de kutu mantığı bozulmaz).
    ///  • Renkler BLOK halinde: aynı renk ART ARDA 3..5 kart (MinRun..MaxRun). Bir destede her renk
    ///    tek blok → ardışık bloklar farklı renk.
    ///  • Renk dağılımı "doubly-balanced" matris: her hücre ∈ {0,3,4,5}, satır=kolon=12.
    ///
    /// KAZANILABİLİRLİK: Kutular yalnızca tam 6'da sevk olduğundan dock'a kart göndermek kuyruğu
    /// bozar → board ZERO-DOCK kazanılabilir olmalı. Önce KARIŞIK blok sırası denenir (`IsWinnable`
    /// ile doğrulanır); bulunamazsa BANTLI (rank sırası, kesin zero-dock) dizilime düşülür.
    /// </summary>
    public static class DefaultLevels
    {
        const int Colors = 4;
        const int Stacks = 4;
        const int Height = 12;        // deste yüksekliği (4×12 = 48 kart)
        const int MinRun = 3;         // aynı renk ART ARDA en az
        const int MaxRun = 5;         // aynı renk ART ARDA en fazla
        const int BinCap = 6;
        const int DockCap = 20;
        const int BeltMax = 20;
        const int MaxAttempts = 120;

        static readonly CardColor[] Palette =
            { CardColor.Red, CardColor.Green, CardColor.Blue, CardColor.Yellow };

        public static int Count => 2;

        public static LevelData Get(int index) => Generate(new System.Random());

        /// <summary>
        /// REKLAM FAIL board'u: deste TEPELERİ aktif-OLMAYAN renkler (Blue/Red) ile dolu →
        /// her basış dock'a gider → dock KESİN dolar (kazanılamaz, "fail-bait"). Aktif renkler
        /// (Green/Yellow) tabanda gömülü, erişilmeden dock dolar. Sıra: Green→Yellow→Blue→Red.
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
                Col(CardColor.Blue, CardColor.Red,  CardColor.Green),   // tepe Blue (aktif değil)
                Col(CardColor.Red,  CardColor.Blue, CardColor.Yellow),  // tepe Red
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
        /// REKLAM SUCCESS board'u. Sıra (rank): Green→Yellow→Blue→Red. BANTLI (her deste tepe=erken
        /// rank) → ZERO-DOCK kazanılabilir: doğru oynanışta tüm kartlar biter. Her renk 12, bloklar 4.
        /// </summary>
        public static LevelData GetDemo()
        {
            var order = new List<CardColor>
            { CardColor.Green, CardColor.Yellow, CardColor.Blue, CardColor.Red };

            // top = tepe, bot = taban. Liste index 0 = taban.
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
            // Karışık blok sırası dene → zero-dock kazanılabilirse al.
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var matrix = BalancedMatrix(rng);
                var order = ShuffledOrder(rng);
                var level = BuildFromMatrix(matrix, order, rng, mixed: true);
                if (IsWinnable(level.stacks, order))
                {
                    Debug.Log($"[DefaultLevels] Karışık bloklu level. Sıra: {string.Join(" → ", order)}");
                    return level;
                }
            }

            // Garantili: BANTLI dizilim (rank sırası, kesin zero-dock).
            Debug.LogWarning("[DefaultLevels] Karışık zero-dock bulunamadı; bantlı dizilime düşüldü.");
            return BuildFromMatrix(BalancedMatrix(rng), ShuffledOrder(rng), rng, mixed: false);
        }

        static List<CardColor> ShuffledOrder(System.Random rng)
        {
            var order = new List<CardColor>(Palette);
            Shuffle(order, rng);
            return order;
        }

        // ---------------------------------------------------------------------
        //  RENK DAĞILIMI: doubly-balanced matris (hücre ∈ {0,3,4,5}, satır=kolon=12)
        // ---------------------------------------------------------------------

        /// <summary>
        /// matrix[s][r] = s. destedeki rank-r kart sayısı. Başlangıç: her renk bir desteyi
        /// ATLAR (permütasyon), diğer 3 destede 4'er → satır=kolon=12, hücreler {0,4}. Sonra
        /// sum-koruyan 2×2 takaslarla 3/5 çeşitliliği eklenir (hücreler {0,3,4,5}).
        /// </summary>
        static int[][] BalancedMatrix(System.Random rng)
        {
            var skip = new int[Colors];                 // skip[r] = rank r'nin atladığı deste
            for (int r = 0; r < Colors; r++) skip[r] = r;
            Shuffle(skip, rng);                          // permütasyon → her deste tam 1 renk atlar

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
                // Toplamları koru: s1c1--, s1c2++, s2c1++, s2c2--  (hepsi {3,4,5} aralığında kalmalı).
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
                // Bloklar: (rank, boyut) — boyut = hücre değeri (>0).
                var blocks = new List<int>();            // rank listesi
                for (int r = 0; r < Colors; r++)
                    if (matrix[s][r] > 0) blocks.Add(r);
                if (mixed) Shuffle(blocks, rng);          // karışık sıra; değilse rank-artan (bantlı)

                var topDown = new List<CardColor>(Height);
                foreach (int r in blocks)
                    for (int k = 0; k < matrix[s][r]; k++) topDown.Add(order[r]);

                topDown.Reverse();                        // LevelData: index 0 = en alt
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
        //  ZERO-DOCK KAZANILABİLİRLİK ÇÖZÜCÜSÜ
        // ---------------------------------------------------------------------

        /// <summary>
        /// Hiç dock kullanmadan kazanılabilir mi? Aktif = order'da kart kalan ilk 2 renk.
        /// Tepesi aktif olan deste gönderilir; hiçbir tepe aktif değilse (kart kaldıysa) → hayır.
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
