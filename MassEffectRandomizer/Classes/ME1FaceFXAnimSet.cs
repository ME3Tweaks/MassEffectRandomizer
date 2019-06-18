using Gibbed.IO;
using MassEffectRandomizer.Classes.ClassesThatProveIHateMyLife;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MassEffectRandomizer.Classes
{


    public interface IFaceFXAnimSet
    {
        ME3DataAnimSetStruct Data { get; }
        HeaderStruct Header { get; }

        IExportEntry Export { get; }

        void AddName(string s);
        void CloneEntry(int n);
        void MoveEntry(int n, int m);
        void RemoveEntry(int n);
        void Save();
    }

    public class HeaderStruct
    {
        public string[] Names;
    }

    public class ME3HeaderStruct : HeaderStruct
    {
        public uint Magic;
        public int unk1;
        public int unk2;
        public string Licensee;
        public string Project;
        public int unk3;
        public ushort unk4;
        public HNodeStruct[] Nodes;

        public static readonly HNodeStruct[] fullNodeTable =
        {
            new HNodeStruct {unk1 = 0x1A, unk2 = 1, Name = "FxObject", unk3 = 0},
            new HNodeStruct {unk1 = 0x48, unk2 = 1, Name = "FxAnim", unk3 = 6},
            new HNodeStruct {unk1 = 0x54, unk2 = 1, Name = "FxAnimSet", unk3 = 0},
            new HNodeStruct {unk1 = 0x5F, unk2 = 1, Name = "FxNamedObject", unk3 = 0},
            new HNodeStruct {unk1 = 0x64, unk2 = 1, Name = "FxName", unk3 = 1},
            new HNodeStruct {unk1 = 0x6D, unk2 = 1, Name = "FxAnimCurve", unk3 = 1},
            new HNodeStruct {unk1 = 0x75, unk2 = 1, Name = "FxAnimGroup", unk3 = 0}
        };
    }
    public class HNodeStruct
    {
        public int unk1;
        public int unk2;
        public string Name;
        public ushort unk3;
    }

    public class ME2HeaderStruct : HeaderStruct
    {
        public uint Magic;
        public int unk1;
        public string Licensee;
        public string Project;
        public int unk3;
        public int unk4;
    }

    public class ME1HeaderStruct : HeaderStruct
    {
        public uint Magic;
        public int unk1;
        public string Licensee;
        public string Project;
        public int unk3;
        public ushort unk4;
    }

    public class ME3DataAnimSetStruct
    {
        public int unk1;
        public int unk2;
        public int unk3;
        public int unk4;
        public ME3FaceFXLine[] Data;
    }

    public class ME2DataAnimSetStruct : ME3DataAnimSetStruct
    {
        public int unk5;
        public int unk6;
        public int unk7;
        public int unk8;
        public int unk9;
    }

    public class ME3FaceFXLine
    {
        public int Name;
        public string NameAsString { get; set; }
        public ME3NameRef[] animations;
        public ControlPoint[] points;
        public int[] numKeys;
        public float FadeInTime;
        public float FadeOutTime;
        public int unk2;
        public string path { get; set; }
        public string ID;
        public int index;

        public ME3FaceFXLine Clone()
        {
            ME3FaceFXLine line = (ME3FaceFXLine)MemberwiseClone();
            line.animations = line.animations.TypedClone();
            line.points = line.points.TypedClone();
            line.numKeys = line.numKeys.TypedClone();
            return line;
        }
    }

    public class ME2FaceFXLine : ME3FaceFXLine
    {
        public int unk0;
        public ushort unk1;
        //public int Name;
        public int unk6;
        //public ME2NameRef[] animations;
        //public ControlPoint[] points;
        public ushort unk4;
        //public int[] numKeys;
        //public float FadeInTime;
        //public float FadeOutTime;
        //public int unk2;
        public ushort unk5;
        //public string path { get; set; }
        //public string ID;
        //public int index;
    }

    public class ME3NameRef
    {
        public int index;
        public int unk2;
    }

    public class ME2NameRef : ME3NameRef
    {
        public int unk0;
        public ushort unk1;
        //public int index;
        //public ushort unk2;
        public ushort unk3;
    }

    public struct ControlPoint
    {
        public float time;
        public float weight;
        public float inTangent;
        public float leaveTangent;
    }

    public class ME1FaceFXAnimSet : IFaceFXAnimSet
    {
        ME1Package pcc;
        public IExportEntry export;
        public IExportEntry Export => export;
        ME3HeaderStruct header;
        public HeaderStruct Header => header;
        public ME3DataAnimSetStruct Data { get; private set; }

        public ME1FaceFXAnimSet()
        {
        }
        public ME1FaceFXAnimSet(IExportEntry Entry)
        {
            pcc = Entry.FileRef;
            export = Entry;
            int start = export.propsEnd() + 4;
            SerializingContainer Container = new SerializingContainer(new MemoryStream(export.Data.Skip(start).ToArray()));
            Container.isLoading = true;
            Serialize(Container);
        }

        void Serialize(SerializingContainer Container)
        {
            SerializeHeader(Container);
            SerializeData(Container);
        }

        void SerializeHeader(SerializingContainer Container)
        {
            if (Container.isLoading)
                header = new ME3HeaderStruct();
            header.Magic = Container + header.Magic;
            header.unk1 = Container + header.unk1;
            //header.unk2 = Container + header.unk2;
            int count = 0;
            if (!Container.isLoading)
                count = header.Licensee.Length;
            else
                header.Licensee = "";
            header.Licensee = SerializeString(Container, header.Licensee);
            count = 0;
            if (!Container.isLoading)
                count = header.Project.Length;
            else
                header.Project = "";
            header.Project = SerializeString(Container, header.Project);
            header.unk3 = Container + header.unk3;
            header.unk4 = Container + header.unk4;
            count = 0;
            if (!Container.isLoading)
                count = header.Nodes.Length;
            count = Container + count;
            if (Container.isLoading)
                header.Nodes = new HNodeStruct[count];
            for (int i = 0; i < count; i++)
            {
                if (Container.isLoading)
                    header.Nodes[i] = new HNodeStruct();
                HNodeStruct t = header.Nodes[i];
                t.unk1 = Container + t.unk1;
                t.unk2 = Container + t.unk2;
                t.Name = SerializeString(Container, t.Name);
                t.unk3 = Container + t.unk3;
                header.Nodes[i] = t;
            }
            count = 0;
            if (!Container.isLoading)
                count = header.Names.Length;
            count = Container + count;
            if (Container.isLoading)
                header.Names = new string[count];
            for (int i = 0; i < count; i++)
                header.Names[i] = SerializeString(Container, header.Names[i]);
        }

        void SerializeData(SerializingContainer Container)
        {
            if (Container.isLoading)
                Data = new ME3DataAnimSetStruct();
            Data.unk1 = Container + Data.unk1;
            Data.unk2 = Container + Data.unk2;
            Data.unk3 = Container + Data.unk3;
            Data.unk4 = Container + Data.unk4;
            int count = 0;
            if (!Container.isLoading)
                count = Data.Data.Length;
            count = Container + count;
            if (Container.isLoading)
                Data.Data = new ME3FaceFXLine[count];
            for (int i = 0; i < count; i++)
            {
                if (Container.isLoading)
                    Data.Data[i] = new ME3FaceFXLine();
                ME3FaceFXLine d = Data.Data[i];
                d.Name = Container + d.Name;
                if (Container.isLoading)
                {
                    d.NameAsString = header.Names[d.Name];
                }
                int count2 = 0;
                if (!Container.isLoading)
                    count2 = d.animations.Length;
                count2 = Container + count2;
                if (Container.isLoading)
                    d.animations = new ME3NameRef[count2];
                for (int j = 0; j < count2; j++)
                {
                    if (Container.isLoading)
                        d.animations[j] = new ME3NameRef();
                    ME3NameRef u = d.animations[j];
                    u.index = Container + u.index;
                    u.unk2 = Container + u.unk2;
                    d.animations[j] = u;
                }
                count2 = 0;
                if (!Container.isLoading)
                    count2 = d.points.Length;
                count2 = Container + count2;
                if (Container.isLoading)
                    d.points = new ControlPoint[count2];
                for (int j = 0; j < count2; j++)
                {
                    if (Container.isLoading)
                        d.points[j] = new ControlPoint();
                    ControlPoint u = d.points[j];
                    u.time = Container + u.time;
                    u.weight = Container + u.weight;
                    u.inTangent = Container + u.inTangent;
                    u.leaveTangent = Container + u.leaveTangent;
                    d.points[j] = u;
                }
                if (d.points.Length > 0)
                {
                    count2 = 0;
                    if (!Container.isLoading)
                        count2 = d.numKeys.Length;
                    count2 = Container + count2;
                    if (Container.isLoading)
                        d.numKeys = new int[count2];
                    for (int j = 0; j < count2; j++)
                        d.numKeys[j] = Container + d.numKeys[j];
                }
                else if (Container.isLoading)
                {
                    d.numKeys = new int[d.animations.Length];
                }
                d.FadeInTime = Container + d.FadeInTime;
                d.FadeOutTime = Container + d.FadeOutTime;
                d.unk2 = Container + d.unk2;
                d.path = SerializeString(Container, d.path);
                d.ID = SerializeString(Container, d.ID);
                d.index = Container + d.index;
                Data.Data[i] = d;
            }
        }

        string SerializeString(SerializingContainer Container, string s)
        {
            int len = 0;
            byte t = 0;
            if (Container.isLoading)
            {
                s = "";
                len = Container + len;
                for (int i = 0; i < len; i++)
                    s += (char)(Container + (byte)0);
            }
            else
            {
                len = s.Length;
                len = Container + len;
                foreach (char c in s)
                    t = Container + (byte)c;
            }
            //Debug.WriteLine("Read string of len 0x" + len.ToString("X2") + ": " + s);
            return s;
        }

        //ascii terminated
        string SerializeStringTerminated(SerializingContainer Container, string s)
        {
            int len = 0;
            byte t = 0;
            //ushort unk1 = 1;
            //unk1 = Container + unk1;
            if (Container.isLoading)
            {
                s = "";
                len = Container + len;
                for (int i = 0; i < len; i++)
                    s += (char)(Container + (byte)0);
                Container.Memory.Position += 2; //00 00
            }
            else
            {
                len = s.Length;
                len = Container + len;
                foreach (char c in s)
                    t = Container + (byte)c;
                Container.Memory.Position += 2; //00 00
            }
            return s;
        }

        public void Save()
        {

            MemoryStream m = new MemoryStream();
            SerializingContainer Container = new SerializingContainer(m);
            Container.isLoading = false;
            Serialize(Container);
            m = Container.Memory;
            MemoryStream res = new MemoryStream();
            int start = export.propsEnd();
            res.Write(export.Data, 0, start);
            res.WriteValueS32((int)m.Length);
            res.WriteStream(m);
            res.WriteValueS32(0);
            export.Data = res.ToArray();
        }

        public void CloneEntry(int n)
        {
            if (n < 0 || n >= Data.Data.Length)
                return;
            List<ME3FaceFXLine> list = new List<ME3FaceFXLine>();
            list.AddRange(Data.Data);
            list.Add(Data.Data[n]);
            Data.Data = list.ToArray();
        }
        public void RemoveEntry(int n)
        {
            if (n < 0 || n >= Data.Data.Length)
                return;
            List<ME3FaceFXLine> list = new List<ME3FaceFXLine>();
            list.AddRange(Data.Data);
            list.RemoveAt(n);
            Data.Data = list.ToArray();
        }

        public void MoveEntry(int n, int m)
        {
            if (n < 0 || n >= Data.Data.Length || m < 0 || m >= Data.Data.Length)
                return;
            List<ME3FaceFXLine> list = new List<ME3FaceFXLine>();
            for (int i = 0; i < Data.Data.Length; i++)
                if (i != n)
                    list.Add(Data.Data[i]);
            list.Insert(m, Data.Data[n]);
            Data.Data = list.ToArray();
        }

        public void AddName(string s)
        {
            List<string> list = new List<string>(header.Names);
            list.Add(s);
            header.Names = list.ToArray();
        }
    }
}
