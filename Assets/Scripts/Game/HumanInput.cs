using Tetris.Core;
using UnityEngine.InputSystem;

namespace Tetris.Game
{
    /// <summary>Keyboard input (Input System). Horizontal movement and soft drop have key repeat.</summary>
    public sealed class HumanInput
    {
        const float Das = 0.17f;          // Time before repeat starts
        const float Arr = 0.05f;          // Repeat interval
        const float SoftInterval = 0.03f; // Soft drop interval

        float leftTimer;
        float rightTimer;
        float downTimer;

        public void Update(TetrisGame game, float dt)
        {
            var kb = Keyboard.current;
            if (kb == null || game.IsGameOver) return;

            int left = Repeat(kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame,
                kb.leftArrowKey.isPressed || kb.aKey.isPressed, ref leftTimer, dt);
            int right = Repeat(kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame,
                kb.rightArrowKey.isPressed || kb.dKey.isPressed, ref rightTimer, dt);
            for (int i = 0; i < left; i++) game.MoveLeft();
            for (int i = 0; i < right; i++) game.MoveRight();

            if (kb.upArrowKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) game.Rotate(1);
            if (kb.zKey.wasPressedThisFrame) game.Rotate(-1);

            if (kb.spaceKey.wasPressedThisFrame)
            {
                game.HardDrop();
                return;
            }

            bool down = kb.downArrowKey.isPressed || kb.sKey.isPressed;
            if (down)
            {
                downTimer += dt;
                while (downTimer >= SoftInterval)
                {
                    downTimer -= SoftInterval;
                    game.SoftDrop();
                }
            }
            else
            {
                downTimer = SoftInterval; // Drop one row the moment the key is pressed
            }
        }

        /// <summary>Returns how many times to move this frame: once on press, then once per Arr after Das.</summary>
        static int Repeat(bool pressed, bool held, ref float timer, float dt)
        {
            if (pressed)
            {
                timer = 0;
                return 1;
            }
            if (!held)
            {
                timer = 0;
                return 0;
            }
            int moves = 0;
            timer += dt;
            while (timer >= Das)
            {
                moves++;
                timer -= Arr;
            }
            return moves;
        }
    }
}
