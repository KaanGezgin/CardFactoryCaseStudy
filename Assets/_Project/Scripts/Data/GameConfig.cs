using System;
using UnityEngine;

namespace CardFactory.Data
{
    /// <summary>
    /// Hissi/oynanış ayarları. ScriptableObject DEĞİL: zero-setup için plain
    /// serializable class. GameConfig.Default ile kod-içi defaultlar gelir.
    /// Tüm sayılar buradan veya LevelData'dan ayarlanır; koda dokunmadan tweak.
    /// </summary>
    [Serializable]
    public class GameConfig
    {
        [Header("Konveyör")]
        public float conveyorSpeed = 2.5f;     // birim/sn
        public int beltMaxCards = 20;          // yol limiti (X/20)

        [Header("Kutular")]
        public int activeBinCount = 2;         // daima 2 (sabit)
        public int binCapacity = 10;           // kutu dolum kapasitesi

        [Header("Dock")]
        public int dockCapacity = 20;          // sabit; genişletme yok

        [Header("Juice süreleri (sn)")]
        public float cardSendDuration = 0.25f;
        public float binFillDuration = 0.20f;
        public float popDuration = 0.18f;
        public float warningFlashDuration = 0.30f;

        [Header("Kamera / sahne")]
        public Color backgroundColor = new Color(0.55f, 0.74f, 0.92f); // açık mavi (gerçek oyuna yakın)

        [Header("Reklam efektleri")]
        public bool showHandPointer = true;     // ghost el-pointer
        public bool dockTensionPulse = true;    // dock dolarken kırmızı nabız

        public static GameConfig Default => new GameConfig();
    }
}
