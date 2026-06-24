using System.Collections.Generic;
using UnityEngine;

namespace CardFactory.Data
{
    /// <summary>
    /// Level'leri KODDAN ve RASTGELE üretir (asset yok).
    ///
    /// KARIŞIK (interleaved) + KAZANILABİLİR-BY-CONSTRUCTION:
    ///  • binColorOrder rastgele permütasyon → her renge bir "rank" (0=en erken açılan kutu).
    ///  • Her deste, tepeden-tabana ranklar İÇİN şu invariant'la kurulur:
    ///        bir karttan YUKARIDAKİ her kartın rank'ı ≤ (o kartın rank'ı + 1).
    ///    Yani üst bölge rank 0/1, orta 0/1/2 ve 1/2/3, taban 2/3 → komşu renkler GERÇEKTEN
    ///    iç içe geçer (bantlı değil), ama "en erken renk" daima tepede erişilebilir kalır.
    ///  • Bir renkten ART ARDA en fazla MaxRun (5) kart.
    ///  • Her deste farklı kompozisyon + her oyun farklı sıra → asla aynı görünmez.
    ///
    /// Bu invariant, oyunun aktif-2-kutu mantığında SIFIR DOCK ile kazanmayı garanti eder
    /// (en erken bitmemiş renk hep bir destenin tepesindedir). Güvenlik için üretilen board
    /// `IsWinnable` ile de doğrulanır; (teorik olarak imkânsız ama) geçmezse garantili
    /// bantlı üretime (`BuildLayered`) düşülür.
    /// </summary>
    public static class DefaultLevels
    {
        const int Colors = 4;
        const int Stacks = 4;
        const int Height = 20;      // sütun yüksekliği (4×20 = 80 kart)
        const int MaxRun = 5;       // bir renkten ART ARDA en fazla bu kadar kart
        const int BinCap = 10;
        const int DockCap = 20;
        const int BeltMax = 20;
        const int MaxAttempts = 50;

        static readonly CardColor[] Palette =
            { CardColor.Red, CardColor.Green, CardColor.Blue, CardColor.Yellow };

        public static int Count => 2;

        public static LevelData Get(int index) => Generate(new System.Random());

        static LevelData Generate(System.Random rng)
        {
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var level = BuildInterleaved(rng);
                if (level != null && IsWinnable(level.stacks, level.binColorOrder))
                {
                    Debug.Log($"[DefaultLevels] Karışık level üretildi. Kutu sırası: " +
                              $"{string.Join(" → ", level.binColorOrder)}");
                    return level;
                }
            }
            Debug.LogWarning("[DefaultLevels] Karışık üretim başarısız; garantili bantlı üretime düşüldü.");
            return BuildLayered(rng);
        }

        // ---------------------------------------------------------------------
        //  KARIŞIK ÜRETİM (kazanılabilir-by-construction)
        // ---------------------------------------------------------------------

        static LevelData BuildInterleaved(System.Random rng)
        {
            var order = new List<CardColor>(Palette);
            Shuffle(order, rng);

            var stacks = new List<List<CardColor>>(Stacks);
            for (int s = 0; s < Stacks; s++)
            {
                var targets = RandomComposition(Height, Colors, rng);
                var topDown = BuildStackInvariant(order, targets, rng);
                if (topDown == null) return null;
                topDown.Reverse();                 // LevelData: index 0 = en alt
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

        /// <summary>
        /// Bir desteyi TEPEDEN-TABANA, rank invariant'ıyla kurar (kazanılabilir):
        /// her adımda izinli rank penceresi [maxAbove-1 .. maxAbove+1] → tavan en fazla
        /// 1 yükselir, böylece üst bölge düşük rank kalır. Hedef kompozisyona (targets)
        /// göre ağırlıklı, art-arda ≤MaxRun. Döndürülen liste TEPE → TABAN.
        /// </summary>
        static List<CardColor> BuildStackInvariant(List<CardColor> order, int[] targets, System.Random rng)
        {
            int h = 0;
            foreach (var t in targets) h += t;
            var rem = (int[])targets.Clone();

            var result = new List<CardColor>(h);
            int maxAbove = -1;
            int lastRank = -1, run = 0;

            for (int pos = 0; pos < h; pos++)
            {
                int lo = maxAbove < 0 ? 0 : Mathf.Max(0, maxAbove - 1);
                int hi = maxAbove < 0 ? Mathf.Min(Colors - 1, 1)
                                      : Mathf.Min(Colors - 1, maxAbove + 1);

                // Pencere içindeki adayları topla (art-arda limiti dolu rank hariç).
                int totalW = 0;
                for (int r = lo; r <= hi; r++)
                {
                    if (r == lastRank && run >= MaxRun) continue;
                    totalW += Weight(rem[r]);
                }

                int chosen = -1;
                if (totalW > 0)
                {
                    int pick = rng.Next(totalW);
                    for (int r = lo; r <= hi; r++)
                    {
                        if (r == lastRank && run >= MaxRun) continue;
                        int w = Weight(rem[r]);
                        if (pick < w) { chosen = r; break; }
                        pick -= w;
                    }
                }
                else
                {
                    // Tüm pencere art-arda bloklu (yalnızca tek renk kaldıysa) → ilk uygun.
                    for (int r = lo; r <= hi; r++)
                        if (!(r == lastRank && run >= MaxRun)) { chosen = r; break; }
                }
                if (chosen < 0) return null;

                result.Add(order[chosen]);
                if (rem[chosen] > 0) rem[chosen]--;
                if (chosen > maxAbove) maxAbove = chosen;
                if (chosen == lastRank) run++;
                else { lastRank = chosen; run = 1; }
            }
            return result;
        }

        static int Weight(int remaining) => remaining > 0 ? remaining * 4 : 1;

        /// <summary>20'yi 4 parçaya böler (her parça 2..8 arası), rastgele varyasyonla.</summary>
        static int[] RandomComposition(int total, int parts, System.Random rng)
        {
            var a = new int[parts];
            int baseV = total / parts;
            for (int i = 0; i < parts; i++) a[i] = baseV;
            for (int i = 0; i < total - baseV * parts; i++) a[i % parts]++;

            for (int m = 0; m < 14; m++)
            {
                int from = rng.Next(parts), to = rng.Next(parts);
                if (from != to && a[from] > 2 && a[to] < 8) { a[from]--; a[to]++; }
            }
            return a;
        }

        // ---------------------------------------------------------------------
        //  KAZANILABİLİRLİK SOLVER'I (güvenlik doğrulaması)
        // ---------------------------------------------------------------------

        /// <summary>
        /// Sıfır-dock ile kazanılabilir mi? Aktif kutular = binColorOrder'da bitmemiş
        /// EN ERKEN 2 renk. Tepesi aktif olan deste güvenle gönderilebilir (aktif renk
        /// gönderilince tüm kartları kutuya iner, kutu dolunca aynı renge yeni kutu açılır
        /// → taşma/dock olmaz). Hiçbir tepe aktif değilse ve kart kaldıysa → kazanılamaz.
        /// (Greedy burada hem sağlam hem eksiksizdir: tepe almak yalnızca alttakileri açar.)
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
                foreach (var s in st)
                {
                    if (s.Count == 0) continue;
                    var top = s[s.Count - 1];
                    if (top != a0 && (activeCount < 2 || top != a1)) continue;

                    int g = 0;
                    while (s.Count > 0 && s[s.Count - 1] == top) { s.RemoveAt(s.Count - 1); g++; }
                    remaining[top] -= g;
                    moved = true;
                    break;
                }
                if (!moved) return false;
            }
            return false;
        }

        // ---------------------------------------------------------------------
        //  GARANTİLİ GERİ DÖNÜŞ (eski bantlı üretim)
        // ---------------------------------------------------------------------

        static LevelData BuildLayered(System.Random rng)
        {
            var palette = new List<CardColor>(Palette);

            var perStack = new List<Dictionary<CardColor, int>>();
            var totals = new Dictionary<CardColor, int>();
            foreach (var c in palette) totals[c] = 0;

            for (int s = 0; s < Stacks; s++)
            {
                var counts = DistributeStack(palette, Height, rng);
                foreach (var kv in counts) totals[kv.Key] += kv.Value;
                perStack.Add(counts);
            }

            var order = new List<CardColor>(palette);
            Shuffle(order, rng);
            order.Sort((a, b) => totals[a].CompareTo(totals[b]));

            var stacks = new List<List<CardColor>>();
            for (int s = 0; s < Stacks; s++)
            {
                var st = new List<CardColor>();
                for (int j = Colors - 1; j >= 0; j--)
                {
                    var c = order[j];
                    if (perStack[s].TryGetValue(c, out int n))
                        for (int k = 0; k < n; k++) st.Add(c);
                }
                stacks.Add(st);
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

        static Dictionary<CardColor, int> DistributeStack(List<CardColor> palette, int height, System.Random rng)
        {
            var pal = new List<CardColor>(palette);
            Shuffle(pal, rng);
            int minColors = Mathf.CeilToInt((float)height / MaxRun);
            int subset = Mathf.Clamp(rng.Next(minColors, Colors + 1), minColors, Colors);
            var chosen = pal.GetRange(0, subset);

            var counts = new Dictionary<CardColor, int>();
            foreach (var c in chosen) counts[c] = 0;

            int remaining = height, guard = 0;
            while (remaining > 0 && guard++ < 10000)
            {
                var c = chosen[rng.Next(chosen.Count)];
                if (counts[c] >= MaxRun) continue;
                counts[c]++;
                remaining--;
            }
            return counts;
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
