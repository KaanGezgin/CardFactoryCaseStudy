using System.Collections.Generic;

namespace CardFactory.Data
{
    /// <summary>
    /// Level'leri KODDAN ve RASTGELE üretir (asset yok). ZOR ama kazanılabilir:
    ///  • Her deste rastgele bir renk ALT KÜMESİ (2-4 renk) ve her renkten ≤5
    ///    kartlık bir blok içerir → desteler farklı tepelere sahip olur, bazıları
    ///    "tuzak" (o an aktif olmayan renk tepede) → yanlış basış dock'a düşer.
    ///  • Kutu açılma sırası = elde EN AZ olan renk önce (artan toplam adet).
    ///
    /// Kazanılabilirlik: desteler global O sırasına göre katmanlı (tepe = O'da en
    /// erken renk). Global "en erken kalan renk" daima onu içeren destelerin
    /// tepesindedir; BinManager aktif 2 kutuyu O sırasıyla ilerletir. Yani doğru
    /// desteleri seçen oyuncu sıfır dock ile bitirir; hata yapan dock'u doldurur.
    /// </summary>
    public static class DefaultLevels
    {
        const int Colors = 4;
        const int Stacks = 4;
        const int MaxRun = 5;       // bir renkten art arda en fazla 5 kart
        const int MinRun = 2;

        public static int Count => 2;

        public static LevelData Get(int index) => Generate(new System.Random());

        static LevelData Generate(System.Random rng)
        {
            var palette = new List<CardColor>
            {
                CardColor.Red, CardColor.Green, CardColor.Blue, CardColor.Yellow
            };

            // 1) Her deste: rastgele renk alt kümesi + her renk için [MinRun,MaxRun] adet.
            var perStack = new List<Dictionary<CardColor, int>>();
            var totals = new Dictionary<CardColor, int>();
            foreach (var c in palette) totals[c] = 0;

            for (int s = 0; s < Stacks; s++)
            {
                var pal = new List<CardColor>(palette);
                Shuffle(pal, rng);
                int subset = rng.Next(2, Colors + 1);          // 2..4 renk
                var counts = new Dictionary<CardColor, int>();
                for (int i = 0; i < subset; i++)
                {
                    int size = rng.Next(MinRun, MaxRun + 1);    // 2..5
                    counts[pal[i]] = size;
                    totals[pal[i]] += size;
                }
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
