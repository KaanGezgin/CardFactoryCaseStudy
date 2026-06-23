using System.Collections.Generic;

namespace CardFactory.Data
{
    /// <summary>
    /// Level'leri KODDAN ve RASTGELE üretir (asset yok). Her çağrıda renk sırası
    /// ve dağılım değişir → her oynayış farklı.
    ///
    /// Kazanılabilirlik garantisi: renkler rastgele bir sıraya (O) konur; tüm
    /// yığınlar alttan-üste bu sıranın tersiyle katmanlanır. Böylece her an "en
    /// erken kalan 2 renk" erişilebilir tepelerde olur ve BinManager da aktif 2
    /// kutuyu hep bu 2 renge ayarlar → doğru oynanışta sıfır dock.
    /// </summary>
    public static class DefaultLevels
    {
        // Toplam 120 kart: 4 renk × 30, 4 yığın × 30, kutu kapasitesi 10.
        const int Colors = 4;
        const int Stacks = 4;
        const int Capacity = 10;
        const int PerColor = 30;   // = Stacks başına ortalama; renk başına toplam

        public static int Count => 2;

        public static LevelData Get(int index)
        {
            // index'i tohuma kat ki istersen ileride level'e göre fark yaratılabilsin;
            // şu an her çağrı tamamen rastgele.
            return Generate(new System.Random());
        }

        static LevelData Generate(System.Random rng)
        {
            var palette = new List<CardColor>
            {
                CardColor.Red, CardColor.Green, CardColor.Blue, CardColor.Yellow
            };
            Shuffle(palette, rng);   // O = rastgele renk sırası (açılma sırası)

            // Satır (yığın) ve sütun (renk) toplamları PerColor olan rastgele matris.
            int[,] m = BuildMatrix(rng);

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

        static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// 4×4 matris: her satır ve her sütun toplamı PerColor. Dengeli bir
        /// tabandan, satır/sütun toplamlarını bozmayan rastgele takaslarla karıştırılır.
        /// </summary>
        static int[,] BuildMatrix(System.Random rng)
        {
            // Taban: her satır ve sütun toplamı 30 (8+8+7+7 dönüşümlü).
            int[,] m =
            {
                { 8, 8, 7, 7 },
                { 7, 8, 8, 7 },
                { 7, 7, 8, 8 },
                { 8, 7, 7, 8 },
            };

            // Tek renk run'ı belt limitini (20) aşmasın diye hücre üst sınırı.
            const int MaxCell = 11;

            // Toplamları koruyan takaslar: M[r1,c1]--, M[r2,c2]--, M[r1,c2]++, M[r2,c1]++
            for (int iter = 0; iter < 400; iter++)
            {
                int r1 = rng.Next(Stacks), r2 = rng.Next(Stacks);
                int c1 = rng.Next(Colors), c2 = rng.Next(Colors);
                if (r1 == r2 || c1 == c2) continue;
                if (m[r1, c1] > 0 && m[r2, c2] > 0 &&
                    m[r1, c2] < MaxCell && m[r2, c1] < MaxCell)
                {
                    m[r1, c1]--; m[r2, c2]--;
                    m[r1, c2]++; m[r2, c1]++;
                }
            }
            return m;
        }
    }
}
