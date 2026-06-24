using System.Collections.Generic;

namespace CardFactory.Data
{
    /// <summary>
    /// Level'leri KODDAN ve RASTGELE üretir (asset yok). ZOR ama kazanılabilir:
    ///  • HER SÜTUN AYNI YÜKSEKLİKTE (H kart) → desteler eşit dağılır.
    ///  • Her sütun rastgele 3-4 renklik alt küme ve her renkten ≤5 blok içerir →
    ///    desteler farklı tepelere sahip olur, bazıları "tuzak" → yanlış basış
    ///    dock'a düşer.
    ///  • Kutu açılma sırası = elde EN AZ olan renk önce (artan toplam adet).
    ///
    /// Kazanılabilirlik: desteler global O sırasına göre katmanlı (tepe = O'da en
    /// erken renk). Global "en erken kalan renk" daima onu içeren destelerin
    /// tepesindedir; doğru desteleri seçen oyuncu sıfır dock ile bitirir.
    /// </summary>
    public static class DefaultLevels
    {
        const int Colors = 4;
        const int Stacks = 4;
        const int MaxRun = 5;       // bir renkten art arda en fazla 5 kart
        const int MinHeight = 11;   // sütun yüksekliği alt sınır
        const int MaxHeight = 14;   // sütun yüksekliği üst sınır

        public static int Count => 2;

        public static LevelData Get(int index) => Generate(new System.Random());

        static LevelData Generate(System.Random rng)
        {
            var palette = new List<CardColor>
            {
                CardColor.Red, CardColor.Green, CardColor.Blue, CardColor.Yellow
            };

            int height = rng.Next(MinHeight, MaxHeight + 1);   // TÜM sütunlar bu yükseklikte

            // 1) Her deste: tam "height" kart, 3-4 renklik alt küme, her renk ≤MaxRun.
            var perStack = new List<Dictionary<CardColor, int>>();
            var totals = new Dictionary<CardColor, int>();
            foreach (var c in palette) totals[c] = 0;

            for (int s = 0; s < Stacks; s++)
            {
                var counts = DistributeStack(palette, height, rng);
                foreach (var kv in counts) totals[kv.Key] += kv.Value;
                perStack.Add(counts);
            }

            // 2) Açılma sırası O = en az toplama sahip renk önce (rastgele eşitlik bozucu).
            var order = new List<CardColor>(palette);
            Shuffle(order, rng);
            order.Sort((a, b) => totals[a].CompareTo(totals[b]));

            // 3) Desteleri O sırasına göre kur (alttan-üste O[3..0], tepe = O[0]).
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
                binCapacity = 10,
                dockCapacity = 20,
                beltMaxCards = 20,
                binColorOrder = new List<CardColor>(order),
                stacks = stacks,
            };
        }

        /// <summary>
        /// Tek bir destenin renk dağılımı: tam "height" kart, rastgele 3-4 renk,
        /// her renk en fazla MaxRun. (3 renk × 5 = 15 ≥ MaxHeight olduğundan her
        /// zaman dolar.)
        /// </summary>
        static Dictionary<CardColor, int> DistributeStack(List<CardColor> palette, int height, System.Random rng)
        {
            var pal = new List<CardColor>(palette);
            Shuffle(pal, rng);
            int subset = rng.Next(3, Colors + 1);          // 3 veya 4 renk
            var chosen = pal.GetRange(0, subset);

            var counts = new Dictionary<CardColor, int>();
            foreach (var c in chosen) counts[c] = 0;

            int remaining = height;
            int guard = 0;
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
