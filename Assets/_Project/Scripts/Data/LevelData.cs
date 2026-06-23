using System;
using System.Collections.Generic;

namespace CardFactory.Data
{
    /// <summary>
    /// Tek bir level'in tanımı. Plain serializable class (asset yok).
    /// Yığınlar alttan-üste renk dizisi olarak verilir; kutu kuyruğu sıralı
    /// hedef renk listesidir.
    /// </summary>
    [Serializable]
    public class LevelData
    {
        /// <summary>
        /// Her yığın = alttan-üste renk dizisi (index 0 = en alt).
        /// </summary>
        public List<List<CardColor>> stacks = new();

        /// <summary>
        /// Sevk kutularının hedef renk kuyruğu (eski model; artık binColorOrder
        /// kullanılıyor ama geriye dönük bırakıldı).
        /// </summary>
        public List<CardColor> binQueue = new();

        /// <summary>
        /// Renklerin "açılma" sırası = yığınların üstten-alta erişim sırası.
        /// BinManager kutuları bu sırayla dağıtır; aktif 2 kutu hep farklı renk olur.
        /// </summary>
        public List<CardColor> binColorOrder = new();

        // Level başına override edilebilen sayılar (0/negatif = GameConfig kullan).
        public int activeBinCount = 2;
        public int binCapacity = 10;
        public int dockCapacity = 24;
        public int beltMaxCards = 20;

        /// <summary>Bu level'deki toplam kart sayısı (kazanma kontrolü için).</summary>
        public int TotalCards
        {
            get
            {
                int sum = 0;
                foreach (var s in stacks) sum += s.Count;
                return sum;
            }
        }
    }
}
