using CardFactory.Data;
using UnityEngine;

namespace CardFactory.Core
{
    public enum GameState
    {
        Playing,
        Won,
        Lost
    }

    /// <summary>
    /// Oyun durumunu tutar ve alt sistemleri bağlar. Faz A'da iskelet:
    /// sadece config/level'i yükler ve durumu Playing yapar. Core loop Faz B'de.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameConfig Config { get; private set; }
        public LevelData Level { get; private set; }
        public GameState State { get; private set; } = GameState.Playing;

        public int CurrentLevelIndex { get; private set; }

        public void Init(GameConfig config, int levelIndex)
        {
            Instance = this;
            Config = config ?? GameConfig.Default;
            CurrentLevelIndex = levelIndex;
            Level = DefaultLevels.Get(levelIndex);
            State = GameState.Playing;

            Debug.Log($"[GameManager] Level {levelIndex + 1} yüklendi. " +
                      $"Yığın: {Level.stacks.Count}, Toplam kart: {Level.TotalCards}, " +
                      $"Kutu kuyruğu: {Level.binQueue.Count}");
        }

        // Faz B'de doldurulacak kancalar:
        public void OnDockFull() { /* TODO Faz B: LEVEL FAILED */ }
        public void OnCardShipped() { /* TODO Faz B: kazanma kontrolü */ }
    }
}
