using System;
using System.Collections.Generic;

namespace CardFactory.Data
{
    /// <summary>
    /// Definition of a single level. A plain serializable class (no asset).
    /// Stacks are given as bottom-to-top color sequences; the bin queue is the
    /// ordered list of target colors.
    /// </summary>
    [Serializable]
    public class LevelData
    {
        /// <summary>
        /// Each stack = bottom-to-top color sequence (index 0 = bottom).
        /// </summary>
        public List<List<CardColor>> stacks = new();

        /// <summary>
        /// Target color queue for the ship bins (legacy model; binColorOrder is used now
        /// but this is kept for backward compatibility).
        /// </summary>
        public List<CardColor> binQueue = new();

        /// <summary>
        /// The order in which colors "open up" = the top-to-bottom access order of the stacks.
        /// BinManager hands out bins in this order; the 2 active bins are always different colors.
        /// </summary>
        public List<CardColor> binColorOrder = new();

        // Per-level overridable numbers (0/negative = use GameConfig).
        public int activeBinCount = 2;
        public int binCapacity = 10;
        public int dockCapacity = 24;
        public int beltMaxCards = 20;

        /// <summary>Total number of cards in this level (for the win check).</summary>
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
