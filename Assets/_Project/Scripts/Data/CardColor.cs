using System.Collections.Generic;
using UnityEngine;

namespace CardFactory.Data
{
    /// <summary>
    /// Card color identities. Between 4 and 6 colors. The numeric value is the palette index.
    /// </summary>
    public enum CardColor
    {
        Red = 0,
        Green = 1,
        Blue = 2,
        Yellow = 3,
        Purple = 4,
        Orange = 5
    }

    /// <summary>
    /// CardColor -> UnityEngine.Color mapping. The palette can be overridden via GameConfig;
    /// sensible defaults are kept here.
    /// </summary>
    public static class CardPalette
    {
        // Saturated but easy-on-the-eyes defaults.
        static readonly Dictionary<CardColor, Color> Map = new()
        {
            { CardColor.Red,    new Color(0.90f, 0.22f, 0.24f) },
            { CardColor.Green,  new Color(0.30f, 0.74f, 0.34f) },
            { CardColor.Blue,   new Color(0.20f, 0.50f, 0.90f) },
            { CardColor.Yellow, new Color(0.96f, 0.80f, 0.20f) },
            { CardColor.Purple, new Color(0.62f, 0.34f, 0.80f) },
            { CardColor.Orange, new Color(0.95f, 0.55f, 0.18f) },
        };

        public static Color Get(CardColor color)
        {
            return Map.TryGetValue(color, out var c) ? c : Color.magenta;
        }
    }
}
