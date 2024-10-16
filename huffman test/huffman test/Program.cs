using System;
using System.Collections.Generic;
using System.Linq;

public class HuffmanNode : IComparable<HuffmanNode>
{
    public char Symbol { get; }
    public int Frequency { get; }
    public HuffmanNode Left { get; }
    public HuffmanNode Right { get; }

    public HuffmanNode(char symbol, int frequency, HuffmanNode left = null, HuffmanNode right = null)
    {
        Symbol = symbol;
        Frequency = frequency;
        Left = left;
        Right = right;
    }

    public int CompareTo(HuffmanNode other)
    {
        return Frequency - other.Frequency;
    }
}

class HuffmanCoding
{
    public static Dictionary<char, int> CalculateFrequency(string data)
    {
        return data.GroupBy(c => c)
                   .ToDictionary(g => g.Key, g => g.Count());
    }

    public static List<HuffmanNode> BuildPriorityQueue(Dictionary<char, int> freq)
    {
        List<HuffmanNode> heap = freq.Select(kvp => new HuffmanNode(kvp.Key, kvp.Value)).ToList();
        heap.Sort();
        return heap;
    }

    public static HuffmanNode BuildHuffmanTree(List<HuffmanNode> heap)
    {
        while (heap.Count > 1)
        {
            HuffmanNode lo = heap[0];
            heap.RemoveAt(0);
            HuffmanNode hi = heap[0];
            heap.RemoveAt(0);

            HuffmanNode parent = new HuffmanNode('\0', lo.Frequency + hi.Frequency, lo, hi);
            heap.Add(parent);
            heap.Sort();
        }
        return heap[0];
    }

    public static Dictionary<char, string> GenerateHuffmanCodes(HuffmanNode node, string code = "")
    {
        if (node == null) return new Dictionary<char, string>();

        if (node.Left == null && node.Right == null)
        {
            return new Dictionary<char, string> { { node.Symbol, code } };
        }

        var huffmanCodes = new Dictionary<char, string>();
        huffmanCodes = huffmanCodes.Concat(GenerateHuffmanCodes(node.Left, code + "0")).ToDictionary(k => k.Key, v => v.Value);
        huffmanCodes = huffmanCodes.Concat(GenerateHuffmanCodes(node.Right, code + "1")).ToDictionary(k => k.Key, v => v.Value);
        return huffmanCodes;
    }

    public static void TestHuffmanCoding(string data)
    {
        Console.WriteLine($"\nTesting with data: \"{data}\"");
        var frequency = CalculateFrequency(data);
        var heap = BuildPriorityQueue(frequency);
        var huffmanTree = BuildHuffmanTree(heap);
        var huffmanCodes = GenerateHuffmanCodes(huffmanTree);

        Console.WriteLine("Character Codes:");
        foreach (var kvp in huffmanCodes)
        {
            Console.WriteLine($"{kvp.Key}: {kvp.Value}");
        }

        // Encode Data
        string encodedData = string.Join("", data.Select(c => huffmanCodes[c]));
        Console.WriteLine($"\nEncoded Data: {encodedData}");

        // Decode Data (Reverse mapping)
        var reverseHuffmanCodes = huffmanCodes.ToDictionary(k => k.Value, v => v.Key);
        string decodedData = "";
        string temp = "";

        foreach (var bit in encodedData)
        {
            temp += bit;
            if (reverseHuffmanCodes.ContainsKey(temp))
            {
                decodedData += reverseHuffmanCodes[temp];
                temp = "";
            }
        }

        Console.WriteLine($"\nDecoded Data: {decodedData}");
        Console.WriteLine(decodedData == data ? "Decoding successful!" : "Decoding failed!");
    }

    public static void Main(string[] args)
    {
        // Test cases
        TestHuffmanCoding("hello, this is an example");
        TestHuffmanCoding("a quick brown fox jumps over the lazy dog");
        TestHuffmanCoding("the rain in spain stays mainly in the plain");
        TestHuffmanCoding("aaaabcc");
        TestHuffmanCoding("1234567890");

        Console.WriteLine("\nAll tests completed.");
    }
}
