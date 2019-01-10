﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Xml;

namespace MassEffectRandomizer.Classes.TLK
{
    class HuffmanCompression
    {
        private List<TLKEntry> _inputData = new List<TLKEntry>();
        private Dictionary<char, int> frequencyCount = new Dictionary<char, int>();
        private List<HuffmanNode> _huffmanTree = new List<HuffmanNode>();
        private Dictionary<char, BitArray> _huffmanCodes = new Dictionary<char, BitArray>();

        private class TLKEntry
        {
            public int StringID;
            public int Flags;
            public string data;
            public int index;

            public TLKEntry(int StringID, int flags, string data)
            {
                this.StringID = StringID;
                this.Flags = flags;
                this.data = data;
                index = -1;
            }
        }

        private class HuffmanNode
        {
            public char Data;
            public readonly int FrequencyCount;
            public HuffmanNode Left;
            public HuffmanNode Right;

            public ushort ID;
            public bool leaf;

            public HuffmanNode(char d, int freq)
            {
                leaf = true;
                Data = d;
                FrequencyCount = freq;
            }

            public HuffmanNode(HuffmanNode left, HuffmanNode right)
            {
                leaf = false;
                FrequencyCount = left.FrequencyCount + right.FrequencyCount;
                Left = left;
                Right = right;
            }
        }

        private struct EncodedString
        {
            public int stringLength;
            public int encodedLength;
            public byte[] binaryData;

            public EncodedString(int _stringLength, int _encodedLength, byte[] _data)
            {
                stringLength = _stringLength;
                encodedLength = _encodedLength;
                binaryData = _data;
            }
        }

        /// <summary>
        /// Loads a file into memory and prepares for compressing it to TLK
        /// </summary>
        /// <param name="fileName"></param>
        public void LoadInputData(TalkFile tf)
        {
            _inputData.Clear();
            LoadXmlInputData(tf);
            PrepareHuffmanCoding();
        }

        /// <summary>
        /// Replaces a TLK export with info loaded in this huffman instance
        /// </summary>
        /// <param name="export"></param>
        public void replaceTlkwithFile(IExportEntry export)
        {
            /* converts Huffmann Tree to binary form */
            byte[] treeBuffer = ConvertHuffmanTreeToBuffer();

            List<EncodedString> encodedStrings = new List<EncodedString>();
            int i = 0;
            foreach (var entry in _inputData)
            {
                if (entry.Flags == 0)
                {
                    if (entry.StringID > 0)
                        entry.index = -1;
                    else
                        entry.index = 0;
                }
                else
                {
                    entry.index = i;
                    i++;
                    List<BitArray> binaryData = new List<BitArray>();
                    int binaryLength = 0;
                    /* for every character in a string, put it's binary code into data array */
                    foreach (char c in entry.data)
                    {
                        binaryData.Add(_huffmanCodes[c]);
                        binaryLength += _huffmanCodes[c].Count;
                    }
                    byte[] buffer = BitArrayListToByteArray(binaryData, binaryLength);
                    encodedStrings.Add(new EncodedString(entry.data.Length, buffer.Length, buffer));
                }
            }

            /* get properties from object we're replacing*/
            byte[] properties = export.Data.Take(40).ToArray();

            MemoryStream m = new MemoryStream();

            /* writing properties */
            m.Write(properties, 0, 40);
            m.Seek(0x1C, SeekOrigin.Begin);
            m.Write(BitConverter.GetBytes(_inputData.Count), 0, 4);
            m.Seek(0, SeekOrigin.End);

            /* writing entries */
            m.Write(BitConverter.GetBytes(_inputData.Count), 0, 4);
            foreach (TLKEntry entry in _inputData)
            {
                m.Write(BitConverter.GetBytes(entry.StringID), 0, 4);
                m.Write(BitConverter.GetBytes(entry.Flags), 0, 4);
                m.Write(BitConverter.GetBytes(entry.index), 0, 4);
            }

            /* writing HuffmanTree */
            m.Write(treeBuffer, 0, treeBuffer.Length);

            /* writing data */
            m.Write(BitConverter.GetBytes(encodedStrings.Count), 0, 4);
            foreach (EncodedString enc in encodedStrings)
            {
                m.Write(BitConverter.GetBytes(enc.stringLength), 0, 4);
                m.Write(BitConverter.GetBytes(enc.encodedLength), 0, 4);
                m.Write(enc.binaryData, 0, enc.encodedLength);
            }

            byte[] buff = m.ToArray();
            export.Data = buff;
        }

        /// <summary>
        /// Loads data from XML file into memory
        /// </summary>
        /// <param name="fileName"></param>
        private void LoadXmlInputData(TalkFile tf)
        {
            foreach (TalkFile.TLKStringRef sf in tf.StringRefs)
            {
                int id = sf.StringID;
                int flags = BitConverter.ToInt32(sf.Flags, 0);
                string data = sf.Data;
                if (id > 0)
                {
                    data += '\0';
                }
                _inputData.Add(new TLKEntry(id,flags,data));
            }
        }

        /// <summary>
        /// Creates Huffman Tree based on data from memory.
        /// For every character in text data, a corresponding Huffman Code is prepared.
        /// Source: http://en.wikipedia.org/wiki/Huffman_coding
        /// </summary>
        private void PrepareHuffmanCoding()
        {
            frequencyCount.Clear();
            foreach (var entry in _inputData)
            {
                if (entry.StringID <= 0)
                    continue;
                foreach (char c in entry.data)
                {
                    if (!frequencyCount.ContainsKey(c))
                        frequencyCount.Add(c, 0);
                    ++frequencyCount[c];
                }
            }

            foreach (var element in frequencyCount)
                _huffmanTree.Add(new HuffmanNode(element.Key, element.Value));

            BuildHuffmanTree();
            BuildCodingArray();
        }

        /// <summary>
        /// Standard implementation of building a Huffman Tree
        /// </summary>
        private void BuildHuffmanTree()
        {
            while (_huffmanTree.Count() > 1)
            {
                /* sort Huffman Nodes by frequency */
                _huffmanTree.Sort(CompareNodes);

                HuffmanNode parent = new HuffmanNode(_huffmanTree[0], _huffmanTree[1]);
                _huffmanTree.RemoveAt(0);
                _huffmanTree.RemoveAt(0);
                _huffmanTree.Add(parent);
            }
        }

        /// <summary>
        /// Using Huffman Tree (created with BuildHuffmanTree method), generates a binary code for every character.
        /// </summary>
        private void BuildCodingArray()
        {
            /* stores a binary code */
            List<bool> currentCode = new List<bool>();
            HuffmanNode currenNode = _huffmanTree[0];

            TraverseHuffmanTree(currenNode, currentCode);
        }

        /// <summary>
        /// Recursively traverses Huffman Tree and generates codes
        /// </summary>
        /// <param name="node"></param>
        /// <param name="code"></param>
        private void TraverseHuffmanTree(HuffmanNode node, List<bool> code)
        {
            /* check if both sons are null */
            if (node.Left == node.Right)
            {
                BitArray ba = new BitArray(code.ToArray());
                _huffmanCodes.Add(node.Data, ba);
            }
            else
            {
                /* adds 0 to the code - process left son*/
                code.Add(false);
                TraverseHuffmanTree(node.Left, code);
                code.RemoveAt(code.Count() - 1);

                /* adds 1 to the code - process right son*/
                code.Add(true);
                TraverseHuffmanTree(node.Right, code);
                code.RemoveAt(code.Count() - 1);
            }
        }

        /// <summary>
        /// Converts a Huffman Tree to it's binary representation used by TLK format of Mass Effect 1.
        /// </summary>
        /// <returns></returns>
        private byte[] ConvertHuffmanTreeToBuffer()
        {
            List<HuffmanNode> nodes = new List<HuffmanNode>();
            Queue<HuffmanNode> q = new Queue<HuffmanNode>();

            ushort index = 0;
            q.Enqueue(_huffmanTree[0]);

            while (q.Count > 0)
            {
                HuffmanNode node = q.Dequeue();
                nodes.Add(node);
                node.ID = index;
                index++;
                if (node.Right != null)
                {
                    q.Enqueue(node.Right);
                }
                if (node.Left != null)
                {
                    q.Enqueue(node.Left);
                }
            }

            List<byte> output = new List<byte>();
            output.AddRange(BitConverter.GetBytes((int)index));
            foreach (HuffmanNode node in nodes)
            {
                if (node.leaf)
                {
                    output.Add(1);
                    output.AddRange(BitConverter.GetBytes(node.Data));
                }
                else
                {
                    output.Add(0);
                    output.AddRange(BitConverter.GetBytes(node.Right.ID));
                    output.AddRange(BitConverter.GetBytes(node.Left.ID));
                }
            }

            return output.ToArray();
        }

        /// <summary>
        /// Converts bits in a BitArray to an array with bytes.
        /// Such array is ready to be written to a file.
        /// </summary>
        /// <param name="bitsList"></param>
        /// <param name="bitsCount"></param>
        /// <returns></returns>
        private static byte[] BitArrayListToByteArray(List<BitArray> bitsList, int bitsCount)
        {
            const int BITSPERBYTE = 8;

            int bytesize = bitsCount / BITSPERBYTE;
            if (bitsCount % BITSPERBYTE > 0)
                bytesize++;

            byte[] bytes = new byte[bytesize];
            int bytepos = 0;
            int bitsRead = 0;
            byte value = 0;
            byte significance = 1;

            foreach (BitArray bits in bitsList)
            {
                int bitpos = 0;

                while (bitpos < bits.Length)
                {
                    if (bits[bitpos])
                    {
                        value += significance;
                    }
                    ++bitpos;
                    ++bitsRead;
                    if (bitsRead % BITSPERBYTE == 0)
                    {
                        bytes[bytepos] = value;
                        ++bytepos;
                        value = 0;
                        significance = 1;
                        bitsRead = 0;
                    }
                    else
                    {
                        significance <<= 1;
                    }
                }
            }
            if (bitsRead % BITSPERBYTE != 0)
                bytes[bytepos] = value;
            return bytes;
        }

        /// <summary>
        /// For sorting Huffman Nodes
        /// </summary>
        /// <param name="L1"></param>
        /// <param name="L2"></param>
        /// <returns></returns>
        private static int CompareNodes(HuffmanNode L1, HuffmanNode L2)
        {
            return L1.FrequencyCount.CompareTo(L2.FrequencyCount);
        }
    }
}
