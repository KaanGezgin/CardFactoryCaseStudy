using System.Collections.Generic;

namespace CardFactory.Data
{
    /// <summary>
    /// Level'leri KODDAN ve RASTGELE üretir (asset yok). Her oyunda:
    ///  • Renk açılma sırası O rastgele.
    ///  • HER SÜTUN bağımsız ve rastgele bir kompozisyona sahip: farklı renkler,
    ///    farklı blok boyutları, çoğu zaman farklı tepe rengi → artık tüm sütunlar
    ///    tıpatıp aynı değil.
    ///
    /// Kazanılabilirlik garantisi: aktif kutu sayısı 2 ve her kutu TEK renk
    /// olduğundan, bir an yalnızca 2 renk sevk edilebilir. Bu yüzden her sütun
    /// global O sırasına göre alttan-üste katmanlanır (tepe = O[0]). Bir sütunda en
    /// erken (en küçük O indeksli) renk hep tepededir; BinManager aktif 2 kutuyu O
    /// sırasıyla ilerletir ve bir renk tükendiğinde (kartları stoklarda/yolda
    /// kalmadığında) o renge ait yarım kutuyu da sevk edip slotu bir sonraki renge
    /// açar. Böylece doğru oynanışta sıfır dock.
    /// </summary>
    public static class DefaultLevels
    {
        const int Colors = 4;
        const int Stacks = 4;
        const int MaxRun = 12;      // bir sütundaki tek renk bloğu üst sınırı (belt güvenliği)
        const int MinHeight = 22;   // sütun başına min kart
        const int MaxHeight = 34;   // sütun başına max kart

        public static int Count => 2;

        public static LevelData Get(int index) => Generate(new System.Random());

        static LevelData Generate(System.Random rng)
        {
            var palette = new List<CardColor>
            {
                CardColor.Red, CardColor.Green, CardColor.Blue, CardColor.Yellow
            };
            Shuffle(palette, rng);   // O = rastgele açılma sırası (palette[0] = tepe)

            var stacks = new List<List<CardColor>>();
            for (int s = 0; s < Stacks; s++)
                stacks.Add(BuildStack(palette, rng));

            return new LevelData
            {
                activeBinCount = 2,
                binCapacity = 10,
                dockCapacity = 20,
                beltMaxCards = 20,
                binColorOrder = new List<CardColor>(palette),
                stacks = stacks,
            };
        }

        /// <summary>
        /// Tek bir sütun: rastgele yükseklik, rastgele renk alt kümesi ve rastgele
        /// blok boyutları. Renkler global O sırasına göre dizilir (tepe = O[0]).
        /// </summary>
        static List<CardColor> BuildStack(List<CardColor> palette, System.Random rng)
        {
            int height = rng.Next(MinHeight, MaxHeight + 1);

            // En az kaç renk gerekli (blok <= MaxRun): ceil(height / MaxRun).
            int minColors = (height + MaxRun - 1) / MaxRun;
            int colorCount = rng.Next(System.Math.Max(2, minColors), Colors + 1);

            // Rastgele renk alt kümesi (palette index'leri).
            var idx = new List<int>();
            for (int i = 0; i < Colors; i++) idx.Add(i);
            Shuffle(idx, rng);
            idx = idx.GetRange(0, colorCount);

            // height'i colorCount parçaya böl, her parça [1, MaxRun].
            int[] parts = Partition(height, colorCount, rng);

            // palette index -> bu sütundaki adet
            var perColor = new int[Colors];
            for (int i = 0; i < colorCount; i++) perColor[idx[i]] = parts[i];

            // O-sıralı kur: alttan-üste O[3..0], tepe = O[0].
            var st = new List<CardColor>(height);
            for (int j = Colors - 1; j >= 0; j--)
            {
                int n = perColor[j];
                var color = palette[j];
                for (int k = 0; k < n; k++) st.Add(color);
            }
            return st;
        }

        /// <summary>total'ı k parçaya böler; her parça [1, MaxRun]. Rastgele dağılım.</summary>
        static int[] Partition(int total, int k, System.Random rng)
        {
            var parts = new int[k];
            for (int i = 0; i < k; i++) parts[i] = 1;       // her parça en az 1
            int remaining = total - k;

            int guard = 0;
            while (remaining > 0 && guard++ < 100000)
            {
                int i = rng.Next(k);
                if (parts[i] >= MaxRun) continue;
                parts[i]++;
                remaining--;
            }
            return parts;
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
