using System.Collections.Generic;

namespace CardFactory.Data
{
    /// <summary>
    /// Level'leri KODDAN döndürür (asset yaratmaya gerek yok → zero-setup).
    /// </summary>
    public static class DefaultLevels
    {
        // Kısa yardımcılar (okunabilir level tanımı için).
        static readonly CardColor R = CardColor.Red;
        static readonly CardColor G = CardColor.Green;
        static readonly CardColor B = CardColor.Blue;
        static readonly CardColor Y = CardColor.Yellow;

        public static int Count => 2;

        public static LevelData Get(int index)
        {
            return index switch
            {
                0 => Level1(),
                1 => Level2(),
                _ => Level1(),
            };
        }

        static List<CardColor> Run(CardColor c, int n)
        {
            var list = new List<CardColor>(n);
            for (int i = 0; i < n; i++) list.Add(c);
            return list;
        }

        static List<CardColor> Stack(params List<CardColor>[] runs)
        {
            var s = new List<CardColor>();
            foreach (var r in runs) s.AddRange(r);
            return s;
        }

        /// <summary>
        /// Level 1: 3 renk (R/G/B). Her renk 2 kutu × 10 = 20 kart → toplam 60.
        /// 4 yığına aynı-renk run'ları halinde karışık dağıtılmıştır.
        /// </summary>
        static LevelData Level1()
        {
            var lvl = new LevelData
            {
                activeBinCount = 2,
                binCapacity = 10,
                dockCapacity = 24,
                beltMaxCards = 20,
                binQueue = new List<CardColor> { G, R, B, R, G, B }, // 2'şer kutu
            };

            // 4 yığın, toplam 60 kart (R:20, G:20, B:20). Alttan-üste run'lar.
            lvl.stacks = new List<List<CardColor>>
            {
                Stack(Run(R, 5), Run(G, 5), Run(B, 5)),
                Stack(Run(B, 5), Run(R, 5), Run(G, 5)),
                Stack(Run(G, 5), Run(B, 5), Run(R, 5)),
                Stack(Run(R, 5), Run(B, 5), Run(G, 5)),
            };

            return lvl;
        }

        /// <summary>
        /// Level 2: 4 renk (R/G/B/Y), daha karışık run'lar, aynı kurallar.
        /// Her renk 2 kutu × 10 = 20 kart → toplam 80.
        /// </summary>
        static LevelData Level2()
        {
            var lvl = new LevelData
            {
                activeBinCount = 2,
                binCapacity = 10,
                dockCapacity = 24,
                beltMaxCards = 20,
                binQueue = new List<CardColor> { Y, R, G, B, R, Y, B, G },
            };

            lvl.stacks = new List<List<CardColor>>
            {
                Stack(Run(R, 4), Run(Y, 3), Run(G, 3)),
                Stack(Run(B, 4), Run(R, 4), Run(Y, 2)),
                Stack(Run(Y, 5), Run(B, 3), Run(R, 2)),
                Stack(Run(G, 4), Run(B, 3), Run(R, 3)),
                Stack(Run(B, 5), Run(G, 3), Run(Y, 2)),
            };

            return lvl;
        }
    }
}
