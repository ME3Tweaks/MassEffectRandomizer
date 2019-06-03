using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using Gibbed.IO;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using static MassEffectRandomizer.Classes.ME1Package;

namespace MassEffectRandomizer.Classes
{
    public struct NameReference
    {
        public string Name { get; }
        public int Number { get; }

        public NameReference(string name, int number = 0)
        {
            Name = name;
            Number = number;
        }

        //https://api.unrealengine.com/INT/API/Runtime/Core/UObject/FName/index.html
        public string InstancedString => Number > 0 ? $"{Name}_{Number - 1}" : Name;

        public static implicit operator NameReference(string s)
        {
            return new NameReference(s);
        }

        public static implicit operator string(NameReference n)
        {
            return n.Name;
        }

        public override string ToString()
        {
            return Name ?? string.Empty;
        }

        public static bool operator ==(NameReference r, string s)
        {
            return s == r.Name;
        }

        public static bool operator !=(NameReference r, string s)
        {
            return s != r.Name;
        }
        public bool Equals(NameReference other)
        {
            return string.Equals(Name, other.Name) && Number == other.Number;
        }

        public override bool Equals(object obj)
        {
            return obj is NameReference other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Name.GetHashCode() * 397) ^ Number;
            }
        }
    }

    public enum PropertyType
    {
        Unknown = -1,
        None = 0,
        StructProperty = 1,
        IntProperty = 2,
        FloatProperty = 3,
        ObjectProperty = 4,
        NameProperty = 5,
        BoolProperty = 6,
        ByteProperty = 7,
        ArrayProperty = 8,
        StrProperty = 9,
        StringRefProperty = 10,
        DelegateProperty = 11,
        BioMask4Property
    }

    [Obsolete("Use IExportEntry's GetProperties() instead")]
    public static class PropertyReader
    {
        public static int detectStart(ME1Package pcc, byte[] raw, UnrealFlags.EObjectFlags flags)
        {
            if ((flags & UnrealFlags.EObjectFlags.HasStack) != 0)
            {
                if (pcc.Game != MEGame.ME3)
                {
                    return 32;
                }
                return 30;
            }
            int result = 8;
            int test1 = BitConverter.ToInt32(raw, 4);
            int test2 = BitConverter.ToInt32(raw, 8);
            if (pcc.isName(test1) && test2 == 0)
                result = 4;
            if (pcc.isName(test1) && pcc.isName(test2) && test2 != 0)
                result = 8;
            return result;
        }

        public static void WritePropHeader(this Stream stream, ME1Package pcc, string propName, PropertyType type, int size)
        {
            stream.WriteValueS32(pcc.FindNameOrAdd(propName));
            stream.WriteValueS32(0);
            stream.WriteValueS32(pcc.FindNameOrAdd(type.ToString()));
            stream.WriteValueS32(0);
            stream.WriteValueS32(size);
            stream.WriteValueS32(0);
        }

        public static void WriteNoneProperty(this Stream stream, ME1Package pcc)
        {
            //Debug.WriteLine("Writing none property at 0x" + stream.Position.ToString("X6"));

            stream.WriteValueS32(pcc.FindNameOrAdd("None"));
            stream.WriteValueS32(0);
        }

        public static void WriteStructProperty(this Stream stream, ME1Package pcc, string propName, string structName, MemoryStream value)
        {
            //Debug.WriteLine("Writing struct property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.StructProperty, (int)value.Length);
            stream.WriteValueS32(pcc.FindNameOrAdd(structName));
            stream.WriteValueS32(0);
            stream.WriteStream(value);
        }

        public static void WriteStructProperty(this Stream stream, ME1Package pcc, string propName, string structName, Func<MemoryStream> func)
        {
            stream.WriteStructProperty(pcc, propName, structName, func());
        }

        public static void WriteIntProperty(this Stream stream, ME1Package pcc, string propName, int value)
        {
            //Debug.WriteLine("Writing int property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.IntProperty, 4);
            stream.WriteValueS32(value);
        }

        public static void WriteFloatProperty(this Stream stream, ME1Package pcc, string propName, float value)
        {
            //Debug.WriteLine("Writing float property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.FloatProperty, 4);
            stream.WriteValueF32(value);
        }

        public static void WriteObjectProperty(this Stream stream, ME1Package pcc, string propName, int value)
        {
            //Debug.WriteLine("Writing bool property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.ObjectProperty, 4);
            stream.WriteValueS32(value);
        }

        public static void WriteNameProperty(this Stream stream, ME1Package pcc, string propName, NameReference value)
        {
            //Debug.WriteLine("Writing name property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.NameProperty, 8);
            stream.WriteValueS32(pcc.FindNameOrAdd(value.Name));
            stream.WriteValueS32(value.Number);
        }

        public static void WriteBoolProperty(this Stream stream, ME1Package pcc, string propName, bool value)
        {
            //Debug.WriteLine("Writing bool property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.BoolProperty, 0);
            if (pcc.Game == MEGame.ME3)
            {
                stream.WriteValueB8(value);
            }
            else
            {
                stream.WriteValueB32(value);
            }
        }

        public static void WriteByteProperty(this Stream stream, ME1Package pcc, string propName, byte value)
        {
            //Debug.WriteLine("Writing byte property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));
            stream.WritePropHeader(pcc, propName, PropertyType.ByteProperty, 1);
            if (pcc.Game == MEGame.ME3)
            {
                stream.WriteValueS32(pcc.FindNameOrAdd("None"));
                stream.WriteValueS32(0);
            }
            stream.WriteByte(value);
        }

        public static void WriteEnumProperty(this Stream stream, ME1Package pcc, string propName, NameReference enumName, NameReference enumValue)
        {
            stream.WritePropHeader(pcc, propName, PropertyType.ByteProperty, 8);
            if (pcc.Game == MEGame.ME3)
            {
                stream.WriteValueS32(pcc.FindNameOrAdd(enumName.Name));
                stream.WriteValueS32(enumName.Number);
            }
            stream.WriteValueS32(pcc.FindNameOrAdd(enumValue.Name));
            stream.WriteValueS32(enumValue.Number);
        }

        public static void WriteArrayProperty(this Stream stream, ME1Package pcc, string propName, int count, MemoryStream value)
        {
            //Debug.WriteLine("Writing array property " + propName + ", count: " + count + " at 0x" + stream.Position.ToString("X6")+", length: "+value.Length);
            stream.WritePropHeader(pcc, propName, PropertyType.ArrayProperty, 4 + (int)value.Length);
            stream.WriteValueS32(count);
            stream.WriteStream(value);
        }

        public static void WriteArrayProperty(this Stream stream, ME1Package pcc, string propName, int count, Func<MemoryStream> func)
        {
            stream.WriteArrayProperty(pcc, propName, count, func());
        }

        public static void WriteStringProperty(this Stream stream, ME1Package pcc, string propName, string value)
        {
            //Debug.WriteLine("Writing string property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));
            int strLen = value.Length == 0 ? 0 : value.Length + 1;
            if (pcc.Game == MEGame.ME3)
            {
                if (propName != null)
                {
                    stream.WritePropHeader(pcc, propName, PropertyType.StrProperty, (strLen * 2) + 4);
                }
                stream.WriteStringUnicode(value);
            }
            else
            {
                stream.WritePropHeader(pcc, propName, PropertyType.StrProperty, strLen + 4);
                stream.WriteStringASCII(value);
            }
        }

        public static void WriteStringRefProperty(this Stream stream, ME1Package pcc, string propName, int value)
        {
            //Debug.WriteLine("Writing stringref property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.StringRefProperty, 4);
            stream.WriteValueS32(value);
        }

        public static void WriteDelegateProperty(this Stream stream, ME1Package pcc, string propName, int unk, NameReference value)
        {
            stream.WritePropHeader(pcc, propName, PropertyType.DelegateProperty, 12);
            stream.WriteValueS32(unk);
            stream.WriteValueS32(pcc.FindNameOrAdd(value.Name));
            stream.WriteValueS32(value.Number);
        }
    }
}
