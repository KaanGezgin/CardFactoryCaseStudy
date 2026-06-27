using System.Collections.Generic;
using CardFactory.Data;
using CardFactory.Feedback;
using CardFactory.Gameplay;
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
    /// Holds the game state. Win: when all stacks are emptied and no cards remain on the belt.
    /// Lose: when the dock fills up.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameConfig Config { get; private set; }
        public LevelData Level { get; private set; }
        public GameState State { get; private set; } = GameState.Playing;

        public int CurrentLevelIndex { get; private set; }
        public int Shipped { get; private set; }

        List<CardStack> stacks;
        Conveyor conveyor;

        public void Init(GameConfig config, int levelIndex, LevelData level)
        {
            Instance = this;
            Config = config ?? GameConfig.Default;
            CurrentLevelIndex = levelIndex;
            Level = level;
            Shipped = 0;
            State = GameState.Playing;
        }

        public void SetSystems(List<CardStack> stackList, Conveyor conveyorRef)
        {
            stacks = stackList;
            conveyor = conveyorRef;
        }

        void Update()
        {
            if (State != GameState.Playing || stacks == null || conveyor == null)
                return;

            if (conveyor.BeltCount != 0) return;
            foreach (var s in stacks)
                if (s != null && !s.IsEmpty) return;

            Win();
        }

        void Win()
        {
            State = GameState.Won;
            Sfx.Play("complete", 1.0f);
            Juice.CameraPunch(0.3f);
        }

        public void OnCardShipped()
        {
            if (State != GameState.Playing) return;
            Shipped++;
        }

        public void OnDockFull()
        {
            if (State != GameState.Playing) return;
            State = GameState.Lost;
            Sfx.Play("fail", 1.1f);
            Sfx.Haptic();
        }

        public void Restart()
        {
            GameBootstrap.RequestRebuild(CurrentLevelIndex);
        }

        public void NextLevel()
        {
            GameBootstrap.RequestRebuild((CurrentLevelIndex + 1) % DefaultLevels.Count);
        }
    }
}
