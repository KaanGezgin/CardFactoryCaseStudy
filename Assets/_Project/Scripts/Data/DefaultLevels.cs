using System.Collections.Generic;

namespace CardFactory.Data
{
    /// <summary>
    /// Level'leri KODDAN ve RASTGELE üretir (asset yok). Her oyunda:
    ///  • Renk açılma sırası O rastgele (hangi renk önce gelir).
    ///  • Renk başına TOPLAM kart sayısı rastgele (kapasitenin katı) → her oyun
    ///    farklı oran. Örn. bir oyun 40 kırmızı/20 yeşil/30 mavi/30 sarı,
    ///    başka oyun bambaşka.
    ///  • Renkler yığınlara DENGESİZ/rastgele dağıtılır → her sütun farklı
    ///    kompozisyon ve farklı tepe rengi (artık tüm sütunlar tıpatıp aynı değil).
    ///
    /// Kazanılabilirlik garantisi: aktif kutu sayısı 2 ve her kutu TEK renk
    /// olduğundan, her an yalnızca 2 renk sevk edilebilir. Bu yüzden her yığın O
    /// sırasına göre alttan-üste katmanlanır (tepe = O[0]). Bir yığında en erken
    /// (en küçük indeksli) renk hep tepededir; BinManager aktif 2 kutuyu O sırasıyla
    /// ilerletir. Böylece bir renk açıktayken o renge sahip her yığının tepesi o
    /// renktir → doğru oynanışta sıfır dock. Renk toplamları kapasitenin katı
    /// olduğundan her kutu tam dolup sevk edilir ve slot bir sonraki renge açılır.
    /// </summary>
    public static class DefaultLevels
    {
        const int Colors = 4;
        const int Stacks = 4;
        const int Capacity = 10;    // kutu kapasitesi
        const int PerStack = 30;    // her sütundaki toplam kart (sabit yükseklik)
        const int MaxRun = 12;      // tek renk bloğu üst sınırı (belt/zamanlama güvenliği)

        // Toplam "birim" sayısı; 1 birim = Capacity kart. 120/10 = 12.
        const int TotalUnits = (Stacks * PerStack) / Capacity;

        public static int Count => 2;

        public static LevelData Get(int index)
        {
            // index şu an kullanılmıyor; her çağrı tamamen rastgele bir level üretir.
            return Generate(new System.Random());
        }

        static LevelData Generate(System.Random rng)
        {
            var palette = new List<CardColor>
            {
                CardColor.Red, CardColor.Green, CardColor.Blue, CardColor.Yellow
            };
            Shuffle(palette, rng);   // O = rastgele açılma sırası

            // palette index'ine göre renk başına toplam kart (kapasitenin katı).
            int[] colorTotals = RandomColorTotals(rng);

            // Satır = yığın, sütun = palette index'i. Önce dengeli/feasible bir taban,
            // sonra toplamları koruyan rastgele takaslarla dengesizleştirme.
            int[,] m = BuildMatrix(colorTotals, rng);

            var stacks = new List<List<CardColor>>();
            for (int s = 0; s < Stacks; s++)
            {
                var st = new List<CardColor>();
                // Alttan-üste: O[3], O[2], O[1], O[0] (tepe = O[0])
                for (int j = Colors - 1; j >= 0; j--)
                {
                    var color = palette[j];
                    int n = m[s, j];
                    for (int k = 0; k < n; k++) st.Add(color);
                }
                stacks.Add(st);
            }

            return new LevelData
            {
                activeBinCount = 2,
                binCapacity = Capacity,
                dockCapacity = 20,
                beltMaxCards = 20,
                binColorOrder = new List<CardColor>(palette),
                stacks = stacks,
            };
        }

        /// <summary>
        /// Her renge en az 1, en fazla MaxUnits birim verir; toplam TotalUnits birim.
        /// 1 birim = Capacity kart. Her oyunda farklı bir renk oranı üretir.
        /// </summary>
        static int[] RandomColorTotals(System.Random rng)
        {
            // Bir renk en fazla Stacks×MaxRun karta sığabilir → birim üst sınırı.
            int maxUnits = (Stacks * MaxRun) / Capacity;   // 4*12/10 = 4

            int[] units = new int[Colors];
            for (int j = 0; j < Colors; j++) units[j] = 1;   // her renk en az 1 birim

            int remaining = TotalUnits - Colors;             // dağıtılacak fazla birim
            int guard = 0;
            while (remaining > 0 && guard++ < 10000)
            {
                int j = rng.Next(Colors);
                if (units[j] >= maxUnits) continue;
                units[j]++;
                remaining--;
            }

            int[] totals = new int[Colors];
            for (int j = 0; j < Colors; j++) totals[j] = units[j] * Capacity;
            return totals;
        }

        /// <summary>
        /// Satır toplamı PerStack, sütun toplamı colorTotals[j], hücre [0,MaxRun]
        /// olan bir matris üretir. Önce dengeli feasible bir taban doldurulur, sonra
        /// satır VE sütun toplamlarını koruyan rastgele 2×2 takaslarla dengesizleştirilir
        /// → her sütun farklı kompozisyon kazanır.
        /// </summary>
        static int[,] BuildMatrix(int[] colorTotals, System.Random rng)
        {
            int[,] m = new int[Stacks, Colors];
            int[] rowRem = new int[Stacks];
            for (int s = 0; s < Stacks; s++) rowRem[s] = PerStack;
            int[] colRem = (int[])colorTotals.Clone();

            // Dengeli feasible taban: her adımda en çok ihtiyacı olan rengi, o renkte
            // dolmamış (hücre < MaxRun) ve en boş satıra koy. Dengeli kaldığı için
            // kapasite/cap sınırlarına erkenden takılmaz.
            int total = 0;
            foreach (var c in colorTotals) total += c;

            for (int t = 0; t < total; t++)
            {
                int bestCol = -1;
                for (int j = 0; j < Colors; j++)
                    if (colRem[j] > 0 && (bestCol < 0 || colRem[j] > colRem[bestCol]))
                        bestCol = j;
                if (bestCol < 0) break;

                int bestRow = -1;
                for (int s = 0; s < Stacks; s++)
                    if (rowRem[s] > 0 && m[s, bestCol] < MaxRun &&
                        (bestRow < 0 || rowRem[s] > rowRem[bestRow]))
                        bestRow = s;
                if (bestRow < 0) break;   // feasible değil (parametre aralığında olmamalı)

                m[bestRow, bestCol]++;
                rowRem[bestRow]--;
                colRem[bestCol]--;
            }

            // Toplamları koruyan takaslar → dengeli tabandan dengesiz/çeşitli dağılıma.
            for (int iter = 0; iter < 600; iter++)
            {
                int r1 = rng.Next(Stacks), r2 = rng.Next(Stacks);
                int c1 = rng.Next(Colors), c2 = rng.Next(Colors);
                if (r1 == r2 || c1 == c2) continue;
                if (m[r1, c1] > 0 && m[r2, c2] > 0 &&
                    m[r1, c2] < MaxRun && m[r2, c1] < MaxRun)
                {
                    m[r1, c1]--; m[r2, c2]--;
                    m[r1, c2]++; m[r2, c1]++;
                }
            }
            return m;
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
