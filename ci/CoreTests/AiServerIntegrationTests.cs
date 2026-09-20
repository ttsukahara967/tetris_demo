using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using Tetris.Core;

namespace Tetris.CiTests
{
    /// <summary>
    /// Plays a whole game through the real Go AI server over HTTP, using the Unity core.
    /// This checks the coordinate contract (board layout, shapes, rotation, x) between Unity and Go.
    /// Skipped unless AI_SERVER_URL is set (for example http://127.0.0.1:8090).
    /// </summary>
    public class AiServerIntegrationTests
    {
        const int Pieces = 1000;

        [Test]
        public void CpuPlaysThroughTheGoServerWithoutMisalignment()
        {
            string baseUrl = Environment.GetEnvironmentVariable("AI_SERVER_URL");
            if (string.IsNullOrEmpty(baseUrl)) Assert.Ignore("AI_SERVER_URL is not set");

            using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, IncludeFields = true };
            var game = new TetrisGame(123);
            int misaligned = 0, unplaceable = 0, played = 0;

            for (int i = 0; i < Pieces && !game.IsGameOver; i++)
            {
                string body = AiProtocol.BuildMoveRequest(game.Board, game.Current, game.PeekNext());
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                var response = http.PostAsync("/api/move", content).GetAwaiter().GetResult();
                string text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                Assert.IsTrue(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {text}");
                var move = JsonSerializer.Deserialize<AiProtocol.MoveResponse>(text, options);

                if (!game.TryAlignRotation(move.rotation))
                {
                    unplaceable++;
                    game.HardDrop();
                    continue;
                }
                while (game.LeftmostX > move.x && game.MoveLeft()) { }
                while (game.LeftmostX < move.x && game.MoveRight()) { }
                if (game.LeftmostX != move.x) misaligned++;
                game.HardDrop();
                played++;
            }

            TestContext.WriteLine($"played={played} lines={game.Lines} score={game.Score} over={game.IsGameOver}");
            Assert.AreEqual(0, misaligned, "the AI's x must always be reachable");
            Assert.AreEqual(0, unplaceable, "the AI's rotation must always be applicable");
            Assert.IsFalse(game.IsGameOver, $"the CPU should survive {Pieces} pieces");
            // A shape or rotation mismatch between Unity and Go would make the CPU stack badly and clear far fewer lines.
            Assert.GreaterOrEqual(game.Lines, 300);
        }
    }
}
