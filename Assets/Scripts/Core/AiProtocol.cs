using System;
using System.Text;

namespace Tetris.Core
{
    /// <summary>Types for talking to the Go AI server's POST /api/move, and the request JSON builder.</summary>
    public static class AiProtocol
    {
        /// <summary>The response. Can be deserialized directly with JsonUtility.</summary>
        [Serializable]
        public class MoveResponse
        {
            public int x;         // Leftmost column of the piece after rotation
            public int rotation;  // 0..3 (number of clockwise rotations)
            public string reasoning;
        }

        /// <summary>
        /// Builds the request JSON. board is 20 rows x 10 columns of 0/1 (row 0 is the top).
        /// JsonUtility cannot emit 2D arrays, so the JSON is assembled by hand.
        /// </summary>
        public static string BuildMoveRequest(Board board, PieceType current, PieceType? next)
        {
            var sb = new StringBuilder(Board.Width * Board.Height * 2 + 96);
            sb.Append("{\"board\":[");
            for (int r = 0; r < Board.Height; r++)
            {
                if (r > 0) sb.Append(',');
                sb.Append('[');
                for (int c = 0; c < Board.Width; c++)
                {
                    if (c > 0) sb.Append(',');
                    sb.Append(board.IsFilled(r, c) ? '1' : '0');
                }
                sb.Append(']');
            }
            sb.Append("],\"currentPiece\":\"").Append(PieceShapes.Name(current)).Append('"');
            if (next.HasValue)
                sb.Append(",\"nextPiece\":\"").Append(PieceShapes.Name(next.Value)).Append('"');
            sb.Append('}');
            return sb.ToString();
        }
    }
}
