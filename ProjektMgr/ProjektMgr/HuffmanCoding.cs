using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjektMgr
{
    public static class HuffmanCoding
    {
        public static Dictionary<int, string> YDataDictionary = new Dictionary<int, string>();
        public static Dictionary<int, string> CbDataDictionary = new Dictionary<int, string>();
        public static Dictionary<int, string> CrDataDictionary = new Dictionary<int, string>();
        public static List<HuffmanNode> BuildPriorityQueue(Dictionary<int, int> frequencies)
        {
            List<HuffmanNode> nodes = frequencies.Select(kvp => new HuffmanNode(kvp.Key, kvp.Value)).ToList();
            nodes.Sort();
            return nodes;
        }

        public static HuffmanNode BuildHuffmanTree(List<HuffmanNode> nodes)
        {
            while (nodes.Count > 1)
            {
                HuffmanNode low = nodes[0];
                nodes.RemoveAt(0);
                HuffmanNode high = nodes[0];
                nodes.RemoveAt(0);

                HuffmanNode parent = new HuffmanNode('\0', low.Frequency + high.Frequency, low, high);
                nodes.Add(parent);
                nodes.Sort();
            }
            return nodes[0];
        }

        public static Dictionary<int, string> GenerateHuffmanCodes(HuffmanNode node, string code = "")
        {
            if (node == null) return new Dictionary<int, string>();

            if (node.Left == null && node.Right == null)
            {
                return new Dictionary<int, string> { { node.Value, code } };
            }

            var huffmanCodes = new Dictionary<int, string>();
            huffmanCodes = huffmanCodes.Concat(GenerateHuffmanCodes(node.Left, code + "0")).ToDictionary(k => k.Key, v => v.Value);
            huffmanCodes = huffmanCodes.Concat(GenerateHuffmanCodes(node.Right, code + "1")).ToDictionary(k => k.Key, v => v.Value);
            return huffmanCodes;
        }
    }
}
