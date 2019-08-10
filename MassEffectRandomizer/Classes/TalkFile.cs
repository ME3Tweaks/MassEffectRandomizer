﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using MassEffectRandomizer.Classes;
using Serilog;

namespace MassEffectRandomizer.Classes.TLK
{
    public class TalkFile : IEquatable<TalkFile>
    {
        #region structs
        public struct HuffmanNode
        {
            public int LeftNodeID;
            public int RightNodeID;
            public char data;

            public HuffmanNode(int r, int l)
                : this()
            {
                RightNodeID = r;
                LeftNodeID = l;
            }

            public HuffmanNode(char c)
                : this()
            {
                data = c;
                LeftNodeID = -1;
                RightNodeID = -1;
            }
        }

        public struct TLKStringRef
        {
            public int StringID;
            public string Data;
            public byte[] Flags;
            public int Index;

            public TLKStringRef(BinaryReader r)
                : this()
            {
                StringID = r.ReadInt32();
                Flags = r.ReadBytes(4);
                Index = r.ReadInt32();
            }
        }
        #endregion

        private List<HuffmanNode> nodes;
        private BitArray Bits;
        private int langRef;
        private int tlkSetIndex;
        public bool Modified;
        public TLKStringRef[] StringRefs;
        public IExportEntry export;

        public int LangRef
        {
            get { return langRef; }
            set { langRef = value; language = export.FileRef.getNameEntry(value); }
        }

        public string language;
        public bool male;

        public string Name { get { return export.ObjectName; } }
        public string BioTlkSetName { get { return tlkSetIndex != -1 ? (export.FileRef.Exports[tlkSetIndex].ObjectName + ".") : null; } }


        #region Constructors
        public TalkFile(IExportEntry export)
        {
            this.export = export;
            tlkSetIndex = -1;
            LoadTlkData();
        }

        //public TalkFile(ME1Package _export.FileRef, int _index, bool _male, int _langRef, int _tlkSetIndex)
        //{
        //    export.FileRef = _export.FileRef;
        //    index = _index;
        //    LangRef = _langRef;
        //    male = _male;
        //    tlkSetIndex = _tlkSetIndex;
        //    LoadTlkData();
        //}
        #endregion

        //ITalkFile
        public string findDataById(int strRefID)
        {
            for (int i = 0; i < StringRefs.Length; i++)
            {
                if (StringRefs[i].StringID == strRefID)
                {
                    return StringRefs[i].Data;
                }
            }
            return "No Data";
        }

        #region IEquatable
        public bool Equals(TalkFile other)
        {
            return (other?.export.UIndex == export.UIndex && other.export.FileRef.FileName == export.FileRef.FileName);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            return Equals(obj as TalkFile);
        }

        public override int GetHashCode()
        {
            return 1;
        }
        #endregion

        #region Load Data
        public void LoadTlkData()
        {
            BinaryReader r = new BinaryReader(new MemoryStream(export.Data), Encoding.Unicode);

            //skip properties
            r.BaseStream.Seek(40, SeekOrigin.Begin);

            //hashtable
            int entryCount = r.ReadInt32();
            StringRefs = new TLKStringRef[entryCount];
            for (int i = 0; i < entryCount; i++)
            {
                StringRefs[i] = new TLKStringRef(r);
            }

            //Huffman tree
            nodes = new List<HuffmanNode>();
            int nodeCount = r.ReadInt32();
            bool leaf;
            for (int i = 0; i < nodeCount; i++)
            {
                leaf = r.ReadBoolean();
                if (leaf)
                {
                    nodes.Add(new HuffmanNode(r.ReadChar()));
                }
                else
                {
                    nodes.Add(new HuffmanNode(r.ReadInt16(), r.ReadInt16()));
                }
            }
            //TraverseHuffmanTree(nodes[0], new List<bool>());

            //encoded data
            int stringCount = r.ReadInt32();
            byte[] data = new byte[r.BaseStream.Length - r.BaseStream.Position];
            r.Read(data, 0, data.Length);
            Bits = new BitArray(data);

            //decompress encoded data with huffman tree
            int offset = 4;
            int size;
            List<string> rawStrings = new List<string>(stringCount);
            while (offset * 8 < Bits.Length)
            {
                size = BitConverter.ToInt32(data, offset);
                offset += 4;
                string s = GetString(offset * 8);
                offset += size + 4;
                rawStrings.Add(s);
            }

            //associate StringIDs with strings
            for (int i = 0; i < StringRefs.Length; i++)
            {
                if (StringRefs[i].Flags[0] == 1)
                {
                    StringRefs[i].Data = rawStrings[StringRefs[i].Index];
                }
            }
        }

        public readonly List<int> TlksIdsToNotUpdate = new List<int>();
        public void replaceString(int id, string newString)
        {
            if (id == 182170) return; //do not modify this string - Pinnacle Station (for DLC loading)
            for (int i = 0; i < StringRefs.Length; i++)
            {
                if (StringRefs[i].StringID == id)
                {
                    //Debug.WriteLine("Setting string " + id + " to " + newString);
                    Log.Information($"Updating {export.ObjectName} string id {id} to {newString.Truncate(135)}{(newString.Length > 135 ? "... (truncated for log)" : "")}");
                    StringRefs[i].Data = newString;
                    StringRefs[i].Flags = BitConverter.GetBytes(newString == null ? 0 : 1); //should port to me3explorer.
                    Modified = true;
                    return;
                }
            }
        }

        /// <summary>
        /// Finds the first null string in the tlk. This can be assigned to since it's a blank string.
        /// </summary>
        /// <returns></returns>
        public int getFirstNullString() => StringRefs.FirstOrDefault(x => x.Data == null && x.StringID > 0).StringID;
        private string GetString(int bitOffset)
        {
            HuffmanNode root = nodes[0];
            HuffmanNode curNode = root;

            string curString = "";
            int i;
            for (i = bitOffset; i < Bits.Length; i++)
            {
                /* reading bits' sequence and decoding it to Strings while traversing Huffman Tree */
                int nextNodeID;
                if (Bits[i])
                    nextNodeID = curNode.RightNodeID;
                else
                    nextNodeID = curNode.LeftNodeID;

                /* it's an internal node - keep looking for a leaf */
                if (nextNodeID >= 0)
                    curNode = nodes[nextNodeID];
                else
                /* it's a leaf! */
                {
                    char c = curNode.data;
                    if (c != '\0')
                    {
                        /* it's not NULL */
                        curString += c;
                        curNode = root;
                        i--;
                    }
                    else
                    {
                        /* it's a NULL terminating processed string, we're done */
                        //skip ahead approximately 9 bytes to the next string
                        return curString;
                    }
                }
            }
            return null;
        }

        private void TraverseHuffmanTree(HuffmanNode node, List<bool> code)
        {
            /* check if both sons are null */
            if (node.LeftNodeID == node.RightNodeID)
            {
                BitArray ba = new BitArray(code.ToArray());
                string c = "";
                foreach (bool b in ba)
                {
                    c += b ? '1' : '0';
                }
            }
            else
            {
                /* adds 0 to the code - process left son*/
                code.Add(false);
                TraverseHuffmanTree(nodes[node.LeftNodeID], code);
                code.RemoveAt(code.Count() - 1);

                /* adds 1 to the code - process right son*/
                code.Add(true);
                TraverseHuffmanTree(nodes[node.RightNodeID], code);
                code.RemoveAt(code.Count() - 1);
            }
        }
        #endregion

        public void saveToFile(string fileName)
        {
            XmlTextWriter xr = new XmlTextWriter(fileName, Encoding.UTF8);
            xr.Formatting = Formatting.Indented;
            xr.Indentation = 4;

            xr.WriteStartDocument();
            xr.WriteStartElement("tlkFile");
            xr.WriteAttributeString("Name", Name);

            for (int i = 0; i < StringRefs.Length; i++)
            {
                xr.WriteStartElement("string");

                xr.WriteStartElement("id");
                xr.WriteValue(StringRefs[i].StringID);
                xr.WriteEndElement(); // </id>

                xr.WriteStartElement("flags");
                xr.WriteValue(BitConverter.ToInt32(StringRefs[i].Flags, 0));
                xr.WriteEndElement(); // </flags>

                if (StringRefs[i].Flags[0] != 1)
                    xr.WriteElementString("data", "-1");
                else
                    xr.WriteElementString("data", StringRefs[i].Data);

                xr.WriteEndElement(); // </string>
            }

            xr.WriteEndElement(); // </tlkFile>
            xr.Flush();
            xr.Close();
        }

        internal void saveToExport()
        {
            HuffmanCompression compressor = new HuffmanCompression();
            compressor.LoadInputData(this);
            compressor.replaceTlkwithFile(export);
        }

        internal TLKStringRef findDataByValue(string value) => StringRefs.FirstOrDefault(x => x.Data == value);
    }
}
