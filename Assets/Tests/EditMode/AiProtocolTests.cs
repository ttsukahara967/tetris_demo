using NUnit.Framework;
using Tetris.Core;
using UnityEngine;

namespace Tetris.Tests
{
    public class AiProtocolTests
    {
        [Test]
        public void RequestJsonHasBoardAndPieces()
        {
            var b = new Board();
            b.Set(19, 0, 5);
            string json = AiProtocol.BuildMoveRequest(b, PieceType.T, PieceType.L);

            StringAssert.StartsWith("{\"board\":[[0,0,0,0,0,0,0,0,0,0],", json);
            StringAssert.Contains("[1,0,0,0,0,0,0,0,0,0]],\"currentPiece\":\"T\",\"nextPiece\":\"L\"}", json);
        }

        [Test]
        public void NextPieceIsOmittedWhenNull()
        {
            string json = AiProtocol.BuildMoveRequest(new Board(), PieceType.I, null);
            StringAssert.DoesNotContain("nextPiece", json);
        }

        [Test]
        public void ResponseParsesWithJsonUtility()
        {
            var resp = JsonUtility.FromJson<AiProtocol.MoveResponse>("{\"x\":3,\"rotation\":2,\"reasoning\":\"ok\"}");
            Assert.AreEqual(3, resp.x);
            Assert.AreEqual(2, resp.rotation);
            Assert.AreEqual("ok", resp.reasoning);
        }
    }
}
