using System;
using UnityEngine;

namespace CardFactory.Data
{
    /// <summary>
    /// Feel/gameplay settings. NOT a ScriptableObject: a plain serializable class for
    /// zero-setup. GameConfig.Default provides the in-code defaults. Every number is tuned
    /// here or in LevelData; tweak without touching code.
    /// </summary>
    [Serializable]
    public class GameConfig
    {
        [Header("Conveyor")]
        public float conveyorSpeed = 4f;       // units/sec (a bit faster)
        public int beltMaxCards = 20;          // belt limit (X/20)

        [Header("Bins")]
        public int activeBinCount = 2;         // always 2 (fixed)
        public int binCapacity = 10;           // bin fill capacity

        [Header("Dock")]
        public int dockCapacity = 20;          // fixed; no expansion

        [Header("Juice durations (sec)")]
        public float cardSendDuration = 0.25f;
        public float binFillDuration = 0.20f;
        public float popDuration = 0.18f;
        public float warningFlashDuration = 0.30f;

        [Header("Camera / scene")]
        public Color backgroundColor = new Color(0.55f, 0.74f, 0.92f); // light blue (close to the real game)

        [Header("Ad effects")]
        public bool showHandPointer = true;     // ghost hand-pointer
        public bool dockTensionPulse = true;    // red pulse while the dock fills
        public bool adMode = false;             // true → auto ad sequence on Play (AdDirector). The 'A' key also starts it.

        public static GameConfig Default => new GameConfig();
    }
}
