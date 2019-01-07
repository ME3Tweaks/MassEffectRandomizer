﻿using System;
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
using System.Windows;

namespace MassEffectRandomizer.Classes
{
    #region PropGrid Properties
    [TypeConverter(typeof(ExpandableObjectConverter))]
    struct ObjectProp
    {
        private string _name;
        private int _nameindex;
        [DesignOnly(true)]
        public string objectName
        {
            get { return _name; }
            set { _name = value; }
        }

        public int index
        {
            get { return _nameindex; }
            set { _nameindex = value; }
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    struct NameProp
    {
        private string _name;
        private int _nameindex;
        [DesignOnly(true)]
        public string name
        {
            get { return _name; }
            set { _name = value; }
        }

        public int nameindex
        {
            get { return _nameindex; }
            set { _nameindex = value; }
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    struct StructProp
    {
        private string _name;
        private int _nameindex;
        private int[] _data;
        [DesignOnly(true)]
        public string name
        {
            get { return _name; }
            set { _name = value; }
        }

        public int[] data
        {
            get { return _data; }
            set { _data = value; }
        }

        public int nameindex
        {
            get { return _nameindex; }
            set { _nameindex = value; }
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    struct ColorProp
    {
        private string _name;
        private int _nameindex;
        private byte _a;
        private byte _r;
        private byte _g;
        private byte _b;
        [DesignOnly(true)]
        public string name
        {
            get { return _name; }
            set { _name = value; }
        }

        public byte Alpha
        {
            get { return _a; }
            set { _a = value; }
        }

        public byte Red
        {
            get { return _r; }
            set { _r = value; }
        }

        public byte Green
        {
            get { return _g; }
            set { _g = value; }
        }

        public byte Blue
        {
            get { return _b; }
            set { _b = value; }
        }

        public int nameindex
        {
            get { return _nameindex; }
            set { _nameindex = value; }
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    struct VectorProp
    {
        private string _name;
        private int _nameindex;
        private float _x;
        private float _y;
        private float _z;
        [DesignOnly(true)]
        public string name
        {
            get { return _name; }
            set { _name = value; }
        }

        public float X
        {
            get { return _x; }
            set { _x = value; }
        }

        public float Y
        {
            get { return _y; }
            set { _y = value; }
        }

        public float Z
        {
            get { return _z; }
            set { _z = value; }
        }

        public int nameindex
        {
            get { return _nameindex; }
            set { _nameindex = value; }
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    struct RotatorProp
    {
        private string _name;
        private int _nameindex;
        private float _pitch;
        private float _yaw;
        private float _roll;
        [DesignOnly(true)]
        public string name
        {
            get { return _name; }
            set { _name = value; }
        }

        public float Pitch
        {
            get { return _pitch; }
            set { _pitch = value; }
        }

        public float Yaw
        {
            get { return _yaw; }
            set { _yaw = value; }
        }

        public float Roll
        {
            get { return _roll; }
            set { _roll = value; }
        }

        public int nameindex
        {
            get { return _nameindex; }
            set { _nameindex = value; }
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    struct LinearColorProp
    {
        private string _name;
        private int _nameindex;
        private float _r;
        private float _g;
        private float _b;
        private float _a;
        [DesignOnly(true)]
        public string name
        {
            get { return _name; }
            set { _name = value; }
        }

        public float Red
        {
            get { return _r; }
            set { _r = value; }
        }

        public float Green
        {
            get { return _g; }
            set { _g = value; }
        }

        public float Blue
        {
            get { return _b; }
            set { _b = value; }
        }

        public float Alpha
        {
            get { return _a; }
            set { _a = value; }
        }

        public int nameindex
        {
            get { return _nameindex; }
            set { _nameindex = value; }
        }
    }
    #endregion

    public struct NameReference : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public int Number { get; set; }

        public static implicit operator NameReference(string s)
        {
            return new NameReference { Name = s };
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

        #region Property Changed Notification
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Notifies listeners when given property is updated.
        /// </summary>
        /// <param name="propertyname">Name of property to give notification for. If called in property, argument can be ignored as it will be default.</param>
        private void OnPropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion
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
        public class Property
        {
            public int Name;
            public PropertyType TypeVal;
            public int Size;
            public int offsetval;
            public int offend;
            public PropertyValue Value;
            public byte[] raw;
        }

        public struct PropertyValue
        {
            public int len;
            public string StringValue;
            public int IntValue;
            public float FloatValue;
            public NameReference NameValue;
            public List<PropertyValue> Array;
        }

        public static Property getPropOrNull(IExportEntry export, string propName)
        {
            List<Property> props = getPropList(export);
            foreach (Property prop in props)
            {
                if (export.FileRef.getNameEntry(prop.Name) == propName)
                {
                    return prop;
                }
            }
            return null;
        }

        public static Property getPropOrNull(ME1Package pcc, byte[] data, int start, string propName)
        {
            List<Property> props = ReadProp(pcc, data, start);
            foreach (Property prop in props)
            {
                if (pcc.getNameEntry(prop.Name) == propName)
                {
                    return prop;
                }
            }
            return null;
        }

        public static List<Property> getPropList(IExportEntry export)
        {
            //Application.DoEvents();
            byte[] data = export.Data;
            int start = detectStart(export.FileRef, data, export.ObjectFlags);
            return ReadProp(export.FileRef, data, start);
        }

        public static string TypeToString(int type)
        {
            switch (type)
            {
                case 1: return "Struct Property";
                case 2: return "Integer Property";
                case 3: return "Float Property";
                case 4: return "Object Property";
                case 5: return "Name Property";
                case 6: return "Bool Property";
                case 7: return "Byte Property";
                case 8: return "Array Property";
                case 9: return "String Property";
                case 10: return "String Ref Property";
                default: return "Unknown/None";
            }
        }

        public static string PropertyToText(Property p, ME1Package pcc)
        {
            string s = "";
            s = "Name: " + pcc.getNameEntry(p.Name);
            s += " Type: " + TypeToString((int)p.TypeVal);
            s += " Size: " + p.Size;
            switch (p.TypeVal)
            {
                case PropertyType.StructProperty:
                    s += " \"" + pcc.getNameEntry(p.Value.IntValue) + "\" with " + p.Value.Array.Count + " bytes";
                    break;
                case PropertyType.IntProperty:
                case PropertyType.ObjectProperty:
                case PropertyType.StringRefProperty:
                    s += " Value: " + p.Value.IntValue;
                    break;
                case PropertyType.BoolProperty:
                    s += " Value: " + (p.raw[24] == 1);
                    break;
                case PropertyType.FloatProperty:
                    s += " Value: " + p.Value.FloatValue;
                    break;
                case PropertyType.NameProperty:
                    s += " " + pcc.getNameEntry(p.Value.IntValue);
                    break;
                case PropertyType.ByteProperty:
                    s += " Value: \"" + p.Value.StringValue + "\" with \"" + pcc.getNameEntry(p.Value.IntValue) + "\"";
                    break;
                case PropertyType.ArrayProperty:
                    s += " bytes"; //Value: " + p.Value.Array.Count.ToString() + " Elements";
                    break;
                case PropertyType.StrProperty:
                    if (p.Value.StringValue.Length == 0)
                        break;
                    s += " Value: " + p.Value.StringValue;
                    break;
            }
            return s;
        }

        public static List<List<Property>> ReadStructArrayProp(ME1Package pcc, Property p)
        {
            List<List<Property>> res = new List<List<Property>>();
            int pos = 28;
            int linkCount = BitConverter.ToInt32(p.raw, 24);
            for (int i = 0; i < linkCount; i++)
            {
                List<Property> p2 = ReadProp(pcc, p.raw, pos);
                for (int j = 0; j < p2.Count(); j++)
                {
                    pos += p2[j].raw.Length;
                }
                res.Add(p2);
            }
            return res;
        }

        public static List<Property> ReadProp(ME1Package pcc, byte[] raw, int start)
        {
            Property p;
            PropertyValue v;
            int sname;
            List<Property> result = new List<Property>();
            int pos = start;
            if (raw.Length - pos < 8)
                return result;
            //int name = (int)BitConverter.ToInt64(raw, pos);
            int name = (int)BitConverter.ToInt32(raw, pos);

            if (!pcc.isName(name))
                return result;
            string t = pcc.getNameEntry(name);
            p = new Property();
            p.Name = name;
            //Debug.WriteLine(t +" at "+start);
            if (t == "None")
            {
                p.TypeVal = PropertyType.None;
                p.offsetval = pos;
                p.Size = 8;
                p.Value = new PropertyValue();
                p.raw = BitConverter.GetBytes((long)name);
                p.offend = pos + 8;
                result.Add(p);
                return result;
            }
            //int type = (int)BitConverter.ToInt64(raw, pos + 8);            
            int type = (int)BitConverter.ToInt32(raw, pos + 8);
            if (!pcc.isName(type))
            {
                return result;
            }
            p.Size = BitConverter.ToInt32(raw, pos + 16);
            if (p.Size < 0 || p.Size >= raw.Length)
            {
                return result;
            }
            string tp = pcc.getNameEntry(type);
            switch (tp)
            {

                case "DelegateProperty":
                    p.TypeVal = PropertyType.DelegateProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = BitConverter.ToInt32(raw, pos + 28);
                    v.len = p.Size;
                    v.Array = new List<PropertyValue>();
                    pos += 24;
                    for (int i = 0; i < p.Size; i++)
                    {
                        PropertyValue v2 = new PropertyValue();
                        if (pos < raw.Length)
                            v2.IntValue = raw[pos];
                        v.Array.Add(v2);
                        pos++;
                    }
                    p.Value = v;
                    break;
                case "ArrayProperty":
                    int count = BitConverter.ToInt32(raw, pos + 24);
                    p.TypeVal = PropertyType.ArrayProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = type;
                    v.len = p.Size - 4;
                    count = v.len;//TODO can be other objects too
                    v.Array = new List<PropertyValue>();
                    pos += 28;
                    for (int i = 0; i < count; i++)
                    {
                        PropertyValue v2 = new PropertyValue();
                        if (pos < raw.Length)
                            v2.IntValue = raw[pos];
                        v.Array.Add(v2);
                        pos++;
                    }
                    p.Value = v;
                    break;
                case "StrProperty":
                    count = BitConverter.ToInt32(raw, pos + 24);
                    p.TypeVal = PropertyType.StrProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = type;
                    v.len = count;
                    pos += 28;
                    string s = "";
                    if (count < 0)
                    {
                        count *= -1;
                        for (int i = 1; i < count; i++)
                        {
                            s += (char)raw[pos];
                            pos += 2;
                        }
                        pos += 2;
                    }
                    else if (count > 0)
                    {
                        for (int i = 1; i < count; i++)
                        {
                            s += (char)raw[pos];
                            pos++;
                        }
                        pos++;
                    }
                    v.StringValue = s;
                    p.Value = v;
                    break;
                case "StructProperty":
                    sname = BitConverter.ToInt32(raw, pos + 24);
                    p.TypeVal = PropertyType.StructProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = sname;
                    v.len = p.Size;
                    v.Array = new List<PropertyValue>();
                    pos += 32;
                    for (int i = 0; i < p.Size; i++)
                    {
                        PropertyValue v2 = new PropertyValue();
                        if (pos < raw.Length)
                            v2.IntValue = raw[pos];
                        v.Array.Add(v2);
                        pos++;
                    }
                    p.Value = v;
                    break;
                case "BioMask4Property":
                    p.TypeVal = PropertyType.ByteProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.len = p.Size;
                    pos += 24;
                    v.IntValue = raw[pos];
                    pos += p.Size;
                    p.Value = v;
                    break;
                case "ByteProperty":
                    sname = BitConverter.ToInt32(raw, pos + 24);
                    p.TypeVal = PropertyType.ByteProperty;
                    v = new PropertyValue();
                    v.len = p.Size;
                    {
                        p.offsetval = pos + 24;
                        if (p.Size != 1)
                        {
                            v.StringValue = pcc.getNameEntry(sname);
                            v.IntValue = sname;
                            pos += 32;
                        }
                        else
                        {
                            v.StringValue = "";
                            v.IntValue = raw[pos + 24];
                            pos += 25;
                        }
                    }
                    p.Value = v;
                    break;
                case "FloatProperty":
                    sname = BitConverter.ToInt32(raw, pos + 24);
                    p.TypeVal = PropertyType.FloatProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.FloatValue = BitConverter.ToSingle(raw, pos + 24);
                    v.len = p.Size;
                    pos += 28;
                    p.Value = v;
                    break;
                case "BoolProperty":
                    p = new Property();
                    p.Name = name;
                    p.TypeVal = PropertyType.BoolProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = raw[pos + 24];
                    v.len = 4;
                    pos += v.len + 24;
                    p.Value = v;
                    break;
                default:
                    p.TypeVal = getType(pcc, type);
                    p.offsetval = pos + 24;
                    p.Value = ReadValue(pcc, raw, pos + 24, type);
                    pos += p.Value.len + 24;
                    break;
            }
            p.raw = new byte[pos - start];
            p.offend = pos;
            if (pos < raw.Length)
                for (int i = 0; i < pos - start; i++)
                    p.raw[i] = raw[start + i];
            result.Add(p);
            if (pos != start) result.AddRange(ReadProp(pcc, raw, pos));
            return result;
        }

        static PropertyType getType(ME1Package pcc, int type)
        {
            switch (pcc.getNameEntry(type))
            {
                case "None": return PropertyType.None;
                case "StructProperty": return PropertyType.StructProperty;
                case "IntProperty": return PropertyType.IntProperty;
                case "FloatProperty": return PropertyType.FloatProperty;
                case "ObjectProperty": return PropertyType.ObjectProperty;
                case "NameProperty": return PropertyType.NameProperty;
                case "BoolProperty": return PropertyType.BoolProperty;
                case "ByteProperty": return PropertyType.ByteProperty;
                case "ArrayProperty": return PropertyType.ArrayProperty;
                case "DelegateProperty": return PropertyType.DelegateProperty;
                case "StrProperty": return PropertyType.StrProperty;
                case "StringRefProperty": return PropertyType.StringRefProperty;
                default:
                    return PropertyType.Unknown;
            }
        }

        static PropertyValue ReadValue(ME1Package pcc, byte[] raw, int start, int type)
        {
            PropertyValue v = new PropertyValue();
            switch (pcc.getNameEntry(type))
            {
                case "IntProperty":
                case "ObjectProperty":
                case "StringRefProperty":
                    v.IntValue = BitConverter.ToInt32(raw, start);
                    v.len = 4;
                    break;
                case "NameProperty":
                    v.IntValue = BitConverter.ToInt32(raw, start);
                    var nameRef = new NameReference();
                    nameRef.Name = pcc.getNameEntry(v.IntValue);
                    nameRef.Number = BitConverter.ToInt32(raw, start + 4);
                    if (nameRef.Number > 0)
                        nameRef.Name += "_" + (nameRef.Number - 1);
                    v.NameValue = nameRef;
                    v.StringValue = nameRef.Name;
                    v.len = 8;
                    break;
            }
            return v;
        }

        public static int detectStart(ME1Package pcc, byte[] raw, ulong flags)
        {
            if ((flags & (ulong)UnrealFlags.EObjectFlags.HasStack) != 0)
            {
                return 32;
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

        //public static void ImportProperty(ME1Package pcc, ME1Package importpcc, Property p, string className, MemoryStream m, bool inStruct = false)
        //{
        //    string name = importpcc.getNameEntry(p.Name);
        //    int idxname = pcc.FindNameOrAdd(name);
        //    m.Write(BitConverter.GetBytes(idxname), 0, 4);
        //    m.Write(new byte[4], 0, 4);
        //    if (name == "None")
        //        return;
        //    string type = importpcc.getNameEntry(BitConverter.ToInt32(p.raw, 8));
        //    int idxtype = pcc.FindNameOrAdd(type);
        //    m.Write(BitConverter.GetBytes(idxtype), 0, 4);
        //    m.Write(new byte[4], 0, 4);
        //    string name2;
        //    int idxname2;
        //    int size, count, pos;
        //    List<Property> Props;
        //    switch (type)
        //    {
        //        case "IntProperty":
        //        case "FloatProperty":
        //        case "ObjectProperty":
        //        case "StringRefProperty":
        //            m.Write(BitConverter.GetBytes(4), 0, 4);
        //            m.Write(new byte[4], 0, 4);
        //            m.Write(BitConverter.GetBytes(p.Value.IntValue), 0, 4);
        //            break;
        //        case "NameProperty":
        //            m.Write(BitConverter.GetBytes(8), 0, 4);
        //            m.Write(new byte[4], 0, 4);
        //            m.Write(BitConverter.GetBytes(pcc.FindNameOrAdd(importpcc.getNameEntry(p.Value.IntValue))), 0, 4);
        //            //preserve index or whatever the second part of a namereference is
        //            m.Write(p.raw, 28, 4);
        //            break;
        //        case "BoolProperty":
        //            m.Write(new byte[8], 0, 8);
        //            m.WriteByte((byte)p.Value.IntValue);
        //            {
        //                m.Write(new byte[3], 0, 3);
        //            }
        //            break;
        //        case "BioMask4Property":
        //            m.Write(BitConverter.GetBytes(p.Size), 0, 4);
        //            m.Write(new byte[4], 0, 4);
        //            m.WriteByte((byte)p.Value.IntValue);
        //            break;
        //        case "ByteProperty":
        //            m.Write(BitConverter.GetBytes(p.Size), 0, 4);
        //            m.Write(new byte[4], 0, 4);
        //            //if (pcc.Game == MEGame.ME3)
        //            //{
        //            //    name2 = importpcc.getNameEntry(BitConverter.ToInt32(p.raw, 24));
        //            //    idxname2 = pcc.FindNameOrAdd(name2);
        //            //    m.Write(BitConverter.GetBytes(idxname2), 0, 4);
        //            //    m.Write(new byte[4], 0, 4);
        //            //}
        //            if (p.Size != 1)
        //            {
        //                m.Write(BitConverter.GetBytes(pcc.FindNameOrAdd(importpcc.getNameEntry(p.Value.IntValue))), 0, 4);
        //                m.Write(new byte[4], 0, 4);
        //            }
        //            else
        //            {
        //                m.WriteByte(Convert.ToByte(p.Value.IntValue));
        //            }
        //            break;
        //        case "DelegateProperty":
        //            size = BitConverter.ToInt32(p.raw, 16);
        //            if (size == 0xC)
        //            {
        //                name2 = importpcc.getNameEntry(BitConverter.ToInt32(p.raw, 28));
        //                idxname2 = pcc.FindNameOrAdd(name2);
        //                m.Write(BitConverter.GetBytes(0xC), 0, 4);
        //                m.Write(new byte[4], 0, 4);
        //                m.Write(new byte[4], 0, 4);
        //                m.Write(BitConverter.GetBytes(idxname2), 0, 4);
        //                m.Write(new byte[4], 0, 4);
        //            }
        //            else
        //            {
        //                m.Write(BitConverter.GetBytes(size), 0, 4);
        //                m.Write(new byte[4], 0, 4);
        //                for (int i = 0; i < size; i++)
        //                    m.WriteByte(p.raw[24 + i]);
        //            }
        //            break;
        //        case "StrProperty":
        //            name2 = p.Value.StringValue;
        //            if (p.Value.StringValue.Length > 0)
        //            {
        //                name2 += '\0';
        //            }
        //            if (p.Value.len < 0)
        //            {
        //                //unicode
        //                m.Write(BitConverter.GetBytes(4 + name2.Length * 2), 0, 4);
        //                m.Write(new byte[4], 0, 4);
        //                m.Write(BitConverter.GetBytes(-name2.Length), 0, 4);
        //                foreach (char c in name2)
        //                {
        //                    m.WriteByte((byte)c);
        //                    m.WriteByte(0);
        //                }
        //            }
        //            else
        //            {
        //                //ascii
        //                m.Write(BitConverter.GetBytes(4 + name2.Length), 0, 4);
        //                m.Write(new byte[4], 0, 4);
        //                m.Write(BitConverter.GetBytes(name2.Length), 0, 4);
        //                foreach (char c in name2)
        //                {
        //                    m.WriteByte((byte)c);
        //                }
        //            }
        //            break;
        //        case "StructProperty":
        //            size = BitConverter.ToInt32(p.raw, 16);
        //            name2 = importpcc.getNameEntry(BitConverter.ToInt32(p.raw, 24));
        //            idxname2 = pcc.FindNameOrAdd(name2);
        //            pos = 32;
        //            Props = new List<Property>();
        //            try
        //            {
        //                Props = ReadProp(importpcc, p.raw, pos);
        //            }
        //            catch (Exception)
        //            {
        //            }
        //            m.Write(BitConverter.GetBytes(size), 0, 4);
        //            m.Write(new byte[4], 0, 4);
        //            m.Write(BitConverter.GetBytes(idxname2), 0, 4);
        //            m.Write(new byte[4], 0, 4);
        //            if (Props.Count == 0 || Props[0].TypeVal == PropertyType.Unknown)
        //            {
        //                for (int i = 0; i < size; i++)
        //                    m.WriteByte(p.raw[32 + i]);
        //            }
        //            else
        //            {
        //                foreach (Property pp in Props)
        //                    ImportProperty(pcc, importpcc, pp, className, m, inStruct);
        //            }
        //            break;
        //        case "ArrayProperty":
        //            size = BitConverter.ToInt32(p.raw, 16);
        //            count = BitConverter.ToInt32(p.raw, 24);
        //            PropertyInfo info = ME1UnrealObjectInfo.getPropertyInfo(className, name, inStruct);
        //            ArrayType arrayType = ME1UnrealObjectInfo.getArrayType(info);
        //            pos = 28;
        //            List<Property> AllProps = new List<Property>();

        //            if (arrayType == ArrayType.Struct)
        //            {
        //                for (int i = 0; i < count; i++)
        //                {
        //                    Props = new List<Property>();
        //                    try
        //                    {
        //                        Props = ReadProp(importpcc, p.raw, pos);
        //                    }
        //                    catch (Exception)
        //                    {
        //                    }
        //                    AllProps.AddRange(Props);
        //                    if (Props.Count != 0)
        //                    {
        //                        pos = Props[Props.Count - 1].offend;
        //                    }
        //                }
        //            }
        //            m.Write(BitConverter.GetBytes(size), 0, 4);
        //            m.Write(new byte[4], 0, 4);
        //            m.Write(BitConverter.GetBytes(count), 0, 4);
        //            if (AllProps.Count != 0 && (info == null || !ME1UnrealObjectInfo.isImmutable(info.reference)))
        //            {
        //                foreach (Property pp in AllProps)
        //                    ImportProperty(pcc, importpcc, pp, className, m, inStruct);
        //            }
        //            else if (arrayType == ArrayType.Name)
        //            {
        //                for (int i = 0; i < count; i++)
        //                {
        //                    string s = importpcc.getNameEntry(BitConverter.ToInt32(p.raw, 28 + i * 8));
        //                    m.Write(BitConverter.GetBytes(pcc.FindNameOrAdd(s)), 0, 4);
        //                    //preserve index or whatever the second part of a namereference is
        //                    m.Write(p.raw, 32 + i * 8, 4);
        //                }
        //            }
        //            else
        //            {
        //                m.Write(p.raw, 28, size - 4);
        //            }
        //            break;
        //        default:
        //            throw new Exception(type);
        //    }
        //}

        //public static void ImportImmutableProperty(ME3Package pcc, ME3Package importpcc, Property p, string className, MemoryStream m, bool inStruct = false)
        //{
        //    string name = importpcc.getNameEntry(p.Name);
        //    int idxname = pcc.FindNameOrAdd(name);
        //    if (name == "None")
        //        return;
        //    string type = importpcc.getNameEntry(BitConverter.ToInt32(p.raw, 8));
        //    int idxtype = pcc.FindNameOrAdd(type);
        //    string name2;
        //    int idxname2;
        //    int size, count, pos;
        //    List<Property> Props;
        //    switch (type)
        //    {
        //        case "IntProperty":
        //        case "FloatProperty":
        //        case "ObjectProperty":
        //        case "StringRefProperty":
        //            m.Write(BitConverter.GetBytes(p.Value.IntValue), 0, 4);
        //            break;
        //        case "NameProperty":
        //            m.Write(BitConverter.GetBytes(pcc.FindNameOrAdd(importpcc.getNameEntry(p.Value.IntValue))), 0, 4);
        //            //preserve index or whatever the second part of a namereference is
        //            m.Write(p.raw, 28, 4);
        //            break;
        //        case "BoolProperty":
        //            m.WriteByte((byte)p.Value.IntValue);
        //            break;
        //        case "BioMask4Property":
        //            m.WriteByte((byte)p.Value.IntValue);
        //            break;
        //        case "ByteProperty":
        //            name2 = importpcc.getNameEntry(BitConverter.ToInt32(p.raw, 24));
        //            idxname2 = pcc.FindNameOrAdd(name2);
        //            if (p.Size == 8)
        //            {
        //                m.Write(BitConverter.GetBytes(pcc.FindNameOrAdd(importpcc.getNameEntry(p.Value.IntValue))), 0, 4);
        //                m.Write(new byte[4], 0, 4);
        //            }
        //            else
        //            {
        //                m.WriteByte(p.raw[32]);
        //            }
        //            break;
        //        case "StrProperty":
        //            name2 = p.Value.StringValue;
        //            if (name2.Length > 0)
        //            {
        //                name2 += '\0';
        //            }
        //            m.Write(BitConverter.GetBytes(-name2.Length), 0, 4);
        //            foreach (char c in name2)
        //            {
        //                m.WriteByte((byte)c);
        //                m.WriteByte(0);
        //            }
        //            break;
        //        case "StructProperty":
        //            size = BitConverter.ToInt32(p.raw, 16);
        //            name2 = importpcc.getNameEntry(BitConverter.ToInt32(p.raw, 24));
        //            idxname2 = pcc.FindNameOrAdd(name2);
        //            pos = 32;
        //            Props = new List<Property>();
        //            try
        //            {
        //                Props = ReadProp(importpcc, p.raw, pos);
        //            }
        //            catch (Exception)
        //            {
        //            }
        //            if (Props.Count == 0 || Props[0].TypeVal == PropertyType.Unknown)
        //            {
        //                for (int i = 0; i < size; i++)
        //                    m.WriteByte(p.raw[32 + i]);
        //            }
        //            else
        //            {
        //                foreach (Property pp in Props)
        //                    ImportImmutableProperty(pcc, importpcc, pp, className, m, inStruct);
        //            }
        //            break;
        //        case "ArrayProperty":
        //            size = BitConverter.ToInt32(p.raw, 16);
        //            count = BitConverter.ToInt32(p.raw, 24);
        //            ArrayType arrayType = ME3UnrealObjectInfo.getArrayType(className, importpcc.getNameEntry(p.Name), inStruct);
        //            pos = 28;
        //            List<Property> AllProps = new List<Property>();

        //            if (arrayType == ArrayType.Struct)
        //            {
        //                for (int i = 0; i < count; i++)
        //                {
        //                    Props = new List<Property>();
        //                    try
        //                    {
        //                        Props = ReadProp(importpcc, p.raw, pos);
        //                    }
        //                    catch (Exception)
        //                    {
        //                    }
        //                    AllProps.AddRange(Props);
        //                    if (Props.Count != 0)
        //                    {
        //                        pos = Props[Props.Count - 1].offend;
        //                    }
        //                }
        //            }
        //            m.Write(BitConverter.GetBytes(count), 0, 4);
        //            if (AllProps.Count != 0)
        //            {
        //                foreach (Property pp in AllProps)
        //                    ImportImmutableProperty(pcc, importpcc, pp, className, m, inStruct);
        //            }
        //            else if (arrayType == ArrayType.Name)
        //            {
        //                for (int i = 0; i < count; i++)
        //                {
        //                    string s = importpcc.getNameEntry(BitConverter.ToInt32(p.raw, 28 + i * 8));
        //                    m.Write(BitConverter.GetBytes(pcc.FindNameOrAdd(s)), 0, 4);
        //                    //preserve index or whatever the second part of a namereference is
        //                    m.Write(p.raw, 32 + i * 8, 4);
        //                }
        //            }
        //            else
        //            {
        //                m.Write(p.raw, 28, size - 4);
        //            }
        //            break;
        //        default:
        //        case "DelegateProperty":
        //            throw new NotImplementedException(type);
        //    }
        //}

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

        public static void WriteStructProperty(this Stream stream, ME1Package pcc, string propName, string structName, byte[] value)
        {
            //Debug.WriteLine("Writing struct property " + propName + ", type: " + structName + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.StructProperty, value.Length);
            stream.WriteValueS32(pcc.FindNameOrAdd(structName));
            stream.WriteValueS32(0);
            stream.WriteBytes(value);
        }

        public static void WriteStructProperty(this Stream stream, ME1Package pcc, string propName, string structName, MemoryStream value)
        {
            //Debug.WriteLine("Writing struct property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.StructProperty, (int)value.Length);
            stream.WriteValueS32(pcc.FindNameOrAdd(structName));
            stream.WriteValueS32(0);
            value.WriteTo(stream);// stream.WriteStream(value);
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
            //if (pcc.Game == MEGame.ME3)
            //{
            //    stream.WriteValueB8(value);
            //}
            //else
            //{
                stream.WriteValueB32(value);
            //}
        }

        public static void WriteByteProperty(this Stream stream, ME1Package pcc, string propName, byte value)
        {
            //Debug.WriteLine("Writing byte property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));
            stream.WritePropHeader(pcc, propName, PropertyType.ByteProperty, 1);
            //if (pcc.Game == MEGame.ME3)
            //{
            //    stream.WriteValueS32(pcc.FindNameOrAdd("None"));
            //    stream.WriteValueS32(0);
            //}
            stream.WriteByte(value);
        }

        public static void WriteEnumProperty(this Stream stream, ME1Package pcc, string propName, string enumName, string enumValue, int index = 0)
        {
            stream.WritePropHeader(pcc, propName, PropertyType.ByteProperty, 8);
            //if (pcc.Game == MEGame.ME3)
            //{
            //    stream.WriteValueS32(pcc.FindNameOrAdd(enumName));
            //    stream.WriteValueS32(0);
            //}
            stream.WriteValueS32(pcc.FindNameOrAdd(enumValue));
            stream.WriteValueS32(index);
        }

        public static void WriteEnumProperty(this Stream stream, ME1Package pcc, string propName, NameReference enumName, NameReference enumValue)
        {
            stream.WritePropHeader(pcc, propName, PropertyType.ByteProperty, 8);
            //if (pcc.Game == MEGame.ME3)
            //{
            //    stream.WriteValueS32(pcc.FindNameOrAdd(enumName.Name));
            //    stream.WriteValueS32(enumName.Number);
            //}
            stream.WriteValueS32(pcc.FindNameOrAdd(enumValue.Name));
            stream.WriteValueS32(enumValue.Number);
        }

        public static void WriteArrayProperty(this Stream stream, ME1Package pcc, string propName, int count, byte[] value)
        {
            stream.WritePropHeader(pcc, propName, PropertyType.ArrayProperty, 4 + value.Length);
            stream.WriteValueS32(count);
            stream.WriteBytes(value);
        }

        public static void WriteArrayProperty(this Stream stream, ME1Package pcc, string propName, int count, MemoryStream value)
        {
            //Debug.WriteLine("Writing array property " + propName + ", count: " + count + " at 0x" + stream.Position.ToString("X6")+", length: "+value.Length);
            stream.WritePropHeader(pcc, propName, PropertyType.ArrayProperty, 4 + (int)value.Length);
            stream.WriteValueS32(count);
            value.WriteTo(stream);
//            stream.WriteStream(value);
        }

        public static void WriteArrayProperty(this Stream stream, ME1Package pcc, string propName, int count, Func<MemoryStream> func)
        {
            stream.WriteArrayProperty(pcc, propName, count, func());
        }

        public static void WriteStringProperty(this Stream stream, ME1Package pcc, string propName, string value)
        {
            //Debug.WriteLine("Writing string property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));
            int strLen = value.Length == 0 ? 0 : value.Length + 1;
            //if (pcc.Game == MEGame.ME3)
            //{
            //    if (propName != null)
            //    {
            //        stream.WritePropHeader(pcc, propName, PropertyType.StrProperty, (strLen * 2) + 4);
            //    }
            //    stream.WriteStringUnicode(value);
            //}
            //else
            //{
                stream.WritePropHeader(pcc, propName, PropertyType.StrProperty, strLen + 4);
                stream.WriteStringASCII(value);
           // }
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

        public static void WriteStructPropVector(this Stream stream, ME1Package pcc, string propName, float x, float y, float z)
        {
            MemoryStream m = new MemoryStream(12);
            m.WriteValueF32(x);
            m.WriteValueF32(y);
            m.WriteValueF32(z);
            stream.WriteStructProperty(pcc, propName, "Vector", m.ToArray());
        }
    }
}
