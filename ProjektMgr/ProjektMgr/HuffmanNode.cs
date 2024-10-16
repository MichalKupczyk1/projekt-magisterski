using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjektMgr
{
    public class HuffmanNode : IComparable<HuffmanNode>
    {
        public int Value { get; }
        public int Frequency { get; }
        public HuffmanNode Left { get; }
        public HuffmanNode Right { get; }

        public HuffmanNode(int symbol, int frequency, HuffmanNode left = null, HuffmanNode right = null)
        {
            Value = symbol;
            Frequency = frequency;
            Left = left;
            Right = right;
        }

        public int CompareTo(HuffmanNode other)
        {
            return Frequency - other.Frequency;
        }
    }
}
