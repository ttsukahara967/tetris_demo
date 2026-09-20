using Tetris.Core;
using UnityEngine;

namespace Tetris.Game
{
    /// <summary>
    /// The CPU's controls. Each time a piece spawns, sends the board to the Go AI server,
    /// then moves and drops the piece as the returned (x, rotation) says.
    /// </summary>
    public sealed class CpuPlayer
    {
        /// <summary>Delay between receiving a response and starting to move (so it looks human and the movement is visible).</summary>
        public const float ReactionDelay = 0.25f;
        /// <summary>Interval between one-column horizontal steps.</summary>
        public const float StepInterval = 0.06f;

        readonly TetrisGame game;
        readonly MonoBehaviour host;

        long requestedSerial;
        bool inFlight;
        AiProtocol.MoveResponse plan;
        long planSerial;
        bool aligned;
        float timer;

        public string LastReasoning { get; private set; } = "";
        /// <summary>Error from the most recent AI call. Returns to null after a success.</summary>
        public string Error { get; private set; }

        public CpuPlayer(TetrisGame game, MonoBehaviour host)
        {
            this.game = game;
            this.host = host;
        }

        public void Update(float dt)
        {
            if (game.IsGameOver) return;

            if (!inFlight && requestedSerial != game.PieceSerial) Request();
            if (plan == null || planSerial != game.PieceSerial) return;

            timer += dt;
            if (!aligned)
            {
                if (timer < ReactionDelay) return;
                aligned = true;
                timer = 0;
                // The AI assumes a rotated piece dropped from the top row, so put the piece back into that same state.
                if (!game.TryAlignRotation(plan.rotation))
                {
                    Finish();
                    return;
                }
            }

            while (timer >= StepInterval && plan != null)
            {
                timer -= StepInterval;
                int cur = game.LeftmostX;
                if (cur == plan.x) { Finish(); return; }
                bool moved = cur > plan.x ? game.MoveLeft() : game.MoveRight();
                if (!moved) Finish();
            }
        }

        void Finish()
        {
            plan = null;
            game.HardDrop();
        }

        void Request()
        {
            inFlight = true;
            requestedSerial = game.PieceSerial;
            long serial = requestedSerial;
            string body = AiProtocol.BuildMoveRequest(game.Board, game.Current, game.PeekNext());
            host.StartCoroutine(AiClient.PostMove(
                body,
                resp => OnResponse(serial, resp),
                err => OnError(serial, err)));
        }

        void OnResponse(long serial, AiProtocol.MoveResponse resp)
        {
            inFlight = false;
            if (serial != game.PieceSerial) return; // The piece locked while we were waiting. The next Update will request again.
            Error = null;
            LastReasoning = resp.reasoning ?? "";
            Accept(serial, resp);
        }

        void OnError(long serial, string error)
        {
            inFlight = false;
            Error = error;
            LastReasoning = "";
            if (serial != game.PieceSerial) return;
            // If the server is unreachable, just drop the piece where it is (do not stall the game).
            Accept(serial, new AiProtocol.MoveResponse { x = game.LeftmostX, rotation = game.Rotation });
        }

        void Accept(long serial, AiProtocol.MoveResponse resp)
        {
            plan = resp;
            planSerial = serial;
            aligned = false;
            timer = 0;
        }
    }
}
