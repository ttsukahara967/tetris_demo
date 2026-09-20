using UnityEngine;

namespace Tetris.Game
{
    /// <summary>Starts the game in whichever scene is played, without touching the scene.</summary>
    static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Object.FindAnyObjectByType<AppController>() != null) return;
            new GameObject("TetrisApp").AddComponent<AppController>();
        }
    }
}
