using System;

namespace Tetris.Core
{
    /// <summary>A 7-bag: shuffles all 7 pieces, deals them out, then reshuffles.</summary>
    public sealed class BagRandomizer
    {
        readonly Random rng;
        readonly PieceType[] bag = new PieceType[7];
        int index = 7;

        public BagRandomizer(int seed) => rng = new Random(seed);

        public PieceType Next()
        {
            if (index >= bag.Length)
            {
                for (int i = 0; i < bag.Length; i++) bag[i] = (PieceType)i;
                for (int i = bag.Length - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (bag[i], bag[j]) = (bag[j], bag[i]);
                }
                index = 0;
            }
            return bag[index++];
        }
    }
}
