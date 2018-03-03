using Gibbed.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MassEffectRandomizer.Classes.ME1Package;
using static MassEffectRandomizer.Classes.PropertyCollection;

namespace MassEffectRandomizer.Classes
{
    public class PropertyCollection : ObservableCollection<UProperty>
    {
        static Dictionary<string, PropertyCollection> defaultStructValues = new Dictionary<string, PropertyCollection>();
        public int endOffset;

        /// <summary>
        /// Gets the UProperty with the specified name, returns null if not found
        /// </summary>
        /// <param name="name"></param>
        /// <returns>specified UProperty or null if not found</returns>
        public T GetProp<T>(string name) where T : UProperty
        {
            foreach (var prop in this)
            {
                if (prop.Name.Name == name)
                {
                    return prop as T;
                }
            }
            return null;
        }

        public static PropertyCollection ReadProps(ME1Package pcc, MemoryStream stream, string typeName)
        {
            //DebugOutput.StartDebugger("Property Engine ReadProps()");
            PropertyCollection props = new PropertyCollection();
            long startPosition = stream.Position;
            while (stream.Position + 8 <= stream.Length)
            {
                long nameOffset = stream.Position;
                int nameIdx = stream.ReadValueS32();
                if (!pcc.isName(nameIdx))
                {
                    stream.Seek(-4, SeekOrigin.Current);
                    break;
                }
                string name = pcc.getNameEntry(nameIdx);
                if (name == "None")
                {
                    props.Add(new NoneProperty { PropType = PropertyType.None });
                    stream.Seek(4, SeekOrigin.Current);
                    break;
                }
                //DebugOutput.PrintLn("0x" + nameOffset.ToString("X4") + " " + name);
                NameReference nameRef = new NameReference { Name = name, Number = stream.ReadValueS32() };
                int typeIdx = stream.ReadValueS32();
                stream.Seek(4, SeekOrigin.Current);
                int size = stream.ReadValueS32();
                if (!pcc.isName(typeIdx) || size < 0 || size > stream.Length - stream.Position)
                {
                    stream.Seek(-16, SeekOrigin.Current);
                    break;
                }
                stream.Seek(4, SeekOrigin.Current);
                PropertyType type;
                string namev = pcc.getNameEntry(typeIdx);
                if (Enum.IsDefined(typeof(PropertyType), namev))
                {
                    Enum.TryParse(namev, out type);
                }
                else
                {
                    type = PropertyType.Unknown;
                }
                switch (type)
                {
                    case PropertyType.StructProperty:
                        string structType = pcc.getNameEntry(stream.ReadValueS32());
                        stream.Seek(4, SeekOrigin.Current);
                        PropertyCollection structProps = ReadProps(pcc, stream, structType);
                        props.Add(new StructProperty(structType, structProps, nameRef));
                        break;
                    case PropertyType.IntProperty:
                        props.Add(new IntProperty(stream, nameRef));
                        break;
                    case PropertyType.FloatProperty:
                        props.Add(new FloatProperty(stream, nameRef));
                        break;
                    case PropertyType.ObjectProperty:
                        props.Add(new ObjectProperty(stream, nameRef));
                        break;
                    case PropertyType.NameProperty:
                        props.Add(new NameProperty(stream, pcc, nameRef));
                        break;
                    case PropertyType.BoolProperty:
                        props.Add(new BoolProperty(stream, nameRef));
                        break;
                    case PropertyType.BioMask4Property:
                        props.Add(new BioMask4Property(stream, nameRef));
                        break;
                    case PropertyType.ByteProperty:
                        {
                            if (size != 1)
                            {
                                NameReference enumType = new NameReference();
                                enumType.Name = ME1UnrealObjectInfo.getEnumTypefromProp(typeName, typeName);
                                props.Add(new EnumProperty(stream, pcc, enumType, nameRef));
                            }
                            else
                            {
                                props.Add(new ByteProperty(stream, nameRef));
                            }
                        }
                        break;
                    case PropertyType.ArrayProperty:
                        {
                            props.Add(ReadArrayProperty(stream, pcc, typeName, nameRef));
                        }
                        break;
                    case PropertyType.StrProperty:
                        {
                            props.Add(new StrProperty(stream, nameRef));
                        }
                        break;
                    case PropertyType.StringRefProperty:
                        props.Add(new StringRefProperty(stream, nameRef));
                        break;
                    case PropertyType.DelegateProperty:
                        props.Add(new DelegateProperty(stream, pcc, nameRef));
                        break;
                    case PropertyType.Unknown:
                        {
                            props.Add(new UnknownProperty(stream, size, pcc.getNameEntry(typeIdx), nameRef));
                        }
                        break;
                    case PropertyType.None:
                    default:
                        break;
                }
            }
            if (props.Count > 0)
            {
                if (props[props.Count - 1].PropType != PropertyType.None)
                {
                    stream.Seek(startPosition, SeekOrigin.Begin);
                    return new PropertyCollection { endOffset = (int)stream.Position };
                }
                //remove None Property
                props.RemoveAt(props.Count - 1);
            }
            props.endOffset = (int)stream.Position;
            return props;
        }

        public static PropertyCollection ReadSpecialStruct(ME1Package pcc, MemoryStream stream, string structType, int size)
        {
            PropertyCollection props = new PropertyCollection();
            //TODO: implement getDefaultClassValue() for ME1 and ME2 so this isn't needed
            if (structType == "Rotator")
            {
                string[] labels = { "Pitch", "Yaw", "Roll" };
                for (int i = 0; i < 3; i++)
                {
                    props.Add(new IntProperty(stream, labels[i]));
                }
            }
            else if (structType == "Vector2d" || structType == "RwVector2")
            {
                string[] labels = { "X", "Y" };
                for (int i = 0; i < 2; i++)
                {
                    props.Add(new FloatProperty(stream, labels[i]));
                }
            }
            else if (structType == "Vector" || structType == "RwVector3")
            {
                string[] labels = { "X", "Y", "Z" };
                for (int i = 0; i < 3; i++)
                {
                    props.Add(new FloatProperty(stream, labels[i]));
                }
            }
            else if (structType == "Color")
            {
                string[] labels = { "B", "G", "R", "A" };
                for (int i = 0; i < 4; i++)
                {
                    props.Add(new ByteProperty(stream, labels[i]));
                }
            }
            else if (structType == "LinearColor")
            {
                string[] labels = { "R", "G", "B", "A" };
                for (int i = 0; i < 4; i++)
                {
                    props.Add(new FloatProperty(stream, labels[i]));
                }
            }
            //uses EndsWith to support RwQuat, RwVector4, and RwPlane
            else if (structType.EndsWith("Quat") || structType.EndsWith("Vector4") || structType.EndsWith("Plane"))
            {
                string[] labels = { "X", "Y", "Z", "W" };
                for (int i = 0; i < 4; i++)
                {
                    props.Add(new FloatProperty(stream, labels[i]));
                }
            }
            else if (structType == "TwoVectors")
            {
                string[] labels = { "X", "Y", "Z", "X", "Y", "Z" };
                for (int i = 0; i < 6; i++)
                {
                    props.Add(new FloatProperty(stream, labels[i]));
                }
            }
            else if (structType == "Matrix" || structType == "RwMatrix44")
            {
                string[] labels = { "X Plane", "Y Plane", "Z Plane", "W Plane" };
                string[] labels2 = { "X", "Y", "Z", "W" };
                for (int i = 0; i < 3; i++)
                {
                    PropertyCollection structProps = new PropertyCollection();
                    for (int j = 0; j < 4; j++)
                    {
                        structProps.Add(new FloatProperty(stream, labels2[j]));
                    }
                    props.Add(new StructProperty("Plane", structProps, labels[i], true));
                }
            }
            else if (structType == "Guid")
            {
                string[] labels = { "A", "B", "C", "D" };
                for (int i = 0; i < 4; i++)
                {
                    props.Add(new IntProperty(stream, labels[i]));
                }
            }
            else if (structType == "IntPoint")
            {
                string[] labels = { "X", "Y" };
                for (int i = 0; i < 2; i++)
                {
                    props.Add(new IntProperty(stream, labels[i]));
                }
            }
            else if (structType == "Box" || structType == "BioRwBox")
            {
                string[] labels = { "Min", "Max" };
                string[] labels2 = { "X", "Y", "Z" };
                for (int i = 0; i < 2; i++)
                {
                    PropertyCollection structProps = new PropertyCollection();
                    for (int j = 0; j < 3; j++)
                    {
                        structProps.Add(new FloatProperty(stream, labels2[j]));
                    }
                    props.Add(new StructProperty("Vector", structProps, labels[i], true));
                }
                props.Add(new ByteProperty(stream, "IsValid"));
            }
            else
            {
                props.Add(new UnknownProperty(stream, size));
            }
            return props;
        }

        static UProperty ReadSpecialStructProp(ME1Package pcc, MemoryStream stream, UProperty template, string structType)
        {
            if (stream.Position + 1 >= stream.Length)
            {
                throw new EndOfStreamException("tried to read past bounds of Export Data");
            }
            switch (template.PropType)
            {
                case PropertyType.FloatProperty:
                    return new FloatProperty(stream, template.Name);
                case PropertyType.IntProperty:
                    return new IntProperty(stream, template.Name);
                case PropertyType.ObjectProperty:
                    return new ObjectProperty(stream, template.Name);
                case PropertyType.StringRefProperty:
                    return new StringRefProperty(stream, template.Name);
                case PropertyType.NameProperty:
                    return new NameProperty(stream, pcc, template.Name);
                case PropertyType.BoolProperty:
                    return new BoolProperty(stream, template.Name);
                case PropertyType.ByteProperty:
                    if (template is EnumProperty)
                    {
                        string enumType = ME1UnrealObjectInfo.getEnumTypefromProp(template.Name, structType);
                        return new EnumProperty(stream, pcc, enumType, template.Name);
                    }
                    return new ByteProperty(stream, template.Name);
                case PropertyType.BioMask4Property:
                    return new BioMask4Property(stream, template.Name);
                case PropertyType.StrProperty:
                    return new StrProperty(stream, template.Name);
                case PropertyType.ArrayProperty:
                    return ReadArrayProperty(stream, pcc, structType, template.Name, true);
                case PropertyType.StructProperty:
                    PropertyCollection structProps = ReadSpecialStruct(pcc, stream, ME1UnrealObjectInfo.getPropertyInfo(template.Name, structType).reference, 0);
                    return new StructProperty(structType, structProps, template.Name, true);
                case PropertyType.None:
                    return new NoneProperty(template.Name);
                case PropertyType.DelegateProperty:
                    throw new NotImplementedException("cannot read Delegate property of Immutable struct");
                case PropertyType.Unknown:
                    throw new NotImplementedException("cannot read Unknown property of Immutable struct");
            }
            throw new NotImplementedException("cannot read Unknown property of Immutable struct");
        }

        public static UProperty ReadArrayProperty(MemoryStream stream, ME1Package pcc, string enclosingType, NameReference name, bool IsInImmutable = false)
        {
            long arrayOffset = stream.Position - 24;
            ArrayType arrayType = ME1UnrealObjectInfo.getArrayType(name, enclosingType);
            int count = stream.ReadValueS32();
            switch (arrayType)
            {
                case ArrayType.Object:
                    {
                        var props = new List<ObjectProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            props.Add(new ObjectProperty(stream));
                        }
                        return new ArrayProperty<ObjectProperty>(arrayOffset, props, arrayType, name);
                    }
                case ArrayType.Name:
                    {
                        var props = new List<NameProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            props.Add(new NameProperty(stream, pcc));
                        }
                        return new ArrayProperty<NameProperty>(arrayOffset, props, arrayType, name);
                    }
                case ArrayType.Enum:
                    {
                        var props = new List<EnumProperty>();
                        NameReference enumType = new NameReference { Name = ME1UnrealObjectInfo.getEnumTypefromProp(name, enclosingType) };
                        for (int i = 0; i < count; i++)
                        {
                            props.Add(new EnumProperty(stream, pcc, enumType));
                        }
                        return new ArrayProperty<EnumProperty>(arrayOffset, props, arrayType, name);
                    }
                case ArrayType.Struct:
                    {
                        var props = new List<StructProperty>();
                        string arrayStructType = ME1UnrealObjectInfo.getPropertyInfo(name, enclosingType)?.reference;
                        if (IsInImmutable)
                        {
                            int arraySize = 0;
                            if (!IsInImmutable)
                            {
                                stream.Seek(-16, SeekOrigin.Current);
                                arraySize = stream.ReadValueS32();
                                stream.Seek(12, SeekOrigin.Current);
                            }
                            for (int i = 0; i < count; i++)
                            {
                                PropertyCollection structProps = ReadSpecialStruct(pcc, stream, arrayStructType, arraySize / count);
                                props.Add(new StructProperty(arrayStructType, structProps, isImmutable: true));
                            }
                        }
                        else
                        {
                            for (int i = 0; i < count; i++)
                            {
                                PropertyCollection structProps = ReadProps(pcc, stream, arrayStructType);
                                props.Add(new StructProperty(arrayStructType, structProps));
                            }
                        }
                        return new ArrayProperty<StructProperty>(arrayOffset, props, arrayType, name);
                    }
                case ArrayType.Bool:
                    {
                        var props = new List<BoolProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            props.Add(new BoolProperty(stream));
                        }
                        return new ArrayProperty<BoolProperty>(arrayOffset, props, arrayType, name);
                    }
                case ArrayType.String:
                    {
                        var props = new List<StrProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            props.Add(new StrProperty(stream));
                        }
                        return new ArrayProperty<StrProperty>(arrayOffset, props, arrayType, name);
                    }
                case ArrayType.Float:
                    {
                        var props = new List<FloatProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            props.Add(new FloatProperty(stream));
                        }
                        return new ArrayProperty<FloatProperty>(arrayOffset, props, arrayType, name);
                    }
                case ArrayType.Byte:
                    {
                        var props = new List<ByteProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            props.Add(new ByteProperty(stream));
                        }
                        return new ArrayProperty<ByteProperty>(arrayOffset, props, arrayType, name);
                    }
                case ArrayType.Int:
                default:
                    {
                        var props = new List<IntProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            props.Add(new IntProperty(stream));
                        }
                        return new ArrayProperty<IntProperty>(arrayOffset, props, arrayType, name);
                    }
            }
        }

    }

    public abstract class UProperty
    {
        public PropertyType PropType;
        private NameReference _name;
        public long Offset;

        public NameReference Name
        {
            get { return _name; }
            set { _name = value; }
        }

        protected UProperty(NameReference? name)
        {
            _name = name ?? new NameReference();
        }
    }

    public class NoneProperty : UProperty
    {
        public NoneProperty(NameReference? name = null) : base(name)
        {
            PropType = PropertyType.None;
        }
    }

    public class StructProperty : UProperty
    {
        public readonly bool IsImmutable;
        public string StructType { get; private set; }
        public PropertyCollection Properties { get; private set; }

        public StructProperty(string structType, PropertyCollection props, NameReference? name = null, bool isImmutable = false) : base(name)
        {
            StructType = structType;
            Properties = props;
            IsImmutable = isImmutable;
            PropType = PropertyType.StructProperty;
        }

        public StructProperty(string structType, bool isImmutable, params UProperty[] props) : base(null)
        {
            StructType = structType;
            IsImmutable = isImmutable;
            PropType = PropertyType.StructProperty;
            Properties = new PropertyCollection();
            foreach (var prop in props)
            {
                Properties.Add(prop);
            }

        }

        public T GetProp<T>(string name) where T : UProperty
        {
            return Properties.GetProp<T>(name);
        }
    }

    public class IntProperty : UProperty
    {
        int _value;
        public int Value
        {
            get { return _value; }
            set { _value = value; }
        }

        public IntProperty(MemoryStream stream, NameReference? name = null) : base(name)
        {
            Offset = stream.Position;
            Value = stream.ReadValueS32();
            PropType = PropertyType.IntProperty;
        }

        public IntProperty(int val, NameReference? name = null) : base(name)
        {
            Value = val;
            PropType = PropertyType.IntProperty;
        }

        public static implicit operator IntProperty(int n)
        {
            return new IntProperty(n);
        }

        public static implicit operator int(IntProperty p)
        {
            return p.Value;
        }
    }

    public class FloatProperty : UProperty
    {
        float _value;
        public float Value
        {
            get { return _value; }
            set { _value = value; }
        }

        public FloatProperty(MemoryStream stream, NameReference? name = null) : base(name)
        {
            Offset = stream.Position;
            Value = stream.ReadValueF32();
            PropType = PropertyType.FloatProperty;
        }

        public FloatProperty(float val, NameReference? name = null) : base(name)
        {
            Value = val;
            PropType = PropertyType.FloatProperty;
        }

        public static implicit operator FloatProperty(float n)
        {
            return new FloatProperty(n);
        }

        public static implicit operator float(FloatProperty p)
        {
            return p.Value;
        }
    }

    public class ObjectProperty : UProperty
    {
        int _value;
        public int Value
        {
            get { return _value; }
            set { _value = value; }
        }

        public ObjectProperty(MemoryStream stream, NameReference? name = null) : base(name)
        {
            Offset = stream.Position;
            Value = stream.ReadValueS32();
            PropType = PropertyType.ObjectProperty;
        }

        public ObjectProperty(int val, NameReference? name = null) : base(name)
        {
            Value = val;
            PropType = PropertyType.ObjectProperty;
        }
    }

    public class NameProperty : UProperty
    {
        NameReference _value;
        public NameReference Value
        {
            get { return _value; }
            set { _value = value; }
        }

        public NameProperty(MemoryStream stream, ME1Package pcc, NameReference? name = null) : base(name)
        {
            Offset = stream.Position;
            NameReference nameRef = new NameReference();
            nameRef.Name = pcc.getNameEntry(stream.ReadValueS32());
            nameRef.Number = stream.ReadValueS32();
            Value = nameRef;
            PropType = PropertyType.NameProperty;
        }

        public override string ToString()
        {
            return Value;
        }
    }

    public class BoolProperty : UProperty
    {
        bool _value;
        public bool Value
        {
            get { return _value; }
            set { _value = value; }
        }

        public BoolProperty(MemoryStream stream, NameReference? name = null) : base(name)
        {
            Offset = stream.Position;
            Value = stream.ReadValueB32(); //game = 1
            PropType = PropertyType.BoolProperty;
        }

        public BoolProperty(bool val, NameReference? name = null) : base(name)
        {
            Value = val;
            PropType = PropertyType.BoolProperty;
        }

        public static implicit operator BoolProperty(bool n)
        {
            return new BoolProperty(n);
        }

        public static implicit operator bool(BoolProperty p)
        {
            return p.Value;
        }
    }

    public class ByteProperty : UProperty
    {
        byte _value;
        public byte Value
        {
            get { return _value; }
            set { _value = value; }
        }

        public ByteProperty(byte val, NameReference? name = null) : base(name)
        {
            Value = val;
            PropType = PropertyType.ByteProperty;
        }

        public ByteProperty(MemoryStream stream, NameReference? name = null) : base(name)
        {
            Offset = stream.Position;
            Value = stream.ReadValueU8();
            PropType = PropertyType.ByteProperty;
        }
    }

    public class BioMask4Property : UProperty
    {
        byte _value;
        public byte Value
        {
            get { return _value; }
            set { _value = value; }
        }

        public BioMask4Property(MemoryStream stream, NameReference? name = null) : base(name)
        {
            Offset = stream.Position;
            Value = stream.ReadValueU8();
            PropType = PropertyType.BioMask4Property;
        }
    }

    public class EnumProperty : UProperty
    {
        public NameReference EnumType { get; private set; }
        NameReference _value;
        public NameReference Value
        {
            get { return _value; }
            set { _value = value; }
        }
        public List<string> EnumValues { get; private set; }

        public EnumProperty(MemoryStream stream, ME1Package pcc, NameReference enumType, NameReference? name = null) : base(name)
        {
            Offset = stream.Position;
            EnumType = enumType;
            NameReference enumVal = new NameReference();
            enumVal.Name = pcc.getNameEntry(stream.ReadValueS32());
            enumVal.Number = stream.ReadValueS32();
            Value = enumVal;
            EnumValues = new List<string>();
            PropType = PropertyType.ByteProperty;
        }
    }

    public abstract class ArrayPropertyBase : UProperty
    {
        public abstract IEnumerable<UProperty> ValuesAsProperties { get; }

        protected ArrayPropertyBase(NameReference? name) : base(name)
        {
        }
    }

    public class ArrayProperty<T> : ArrayPropertyBase, IEnumerable<T>, IList<T> where T : UProperty
    {
        public List<T> Values { get; private set; }
        public override IEnumerable<UProperty> ValuesAsProperties => Values.Cast<UProperty>();
        public readonly ArrayType arrayType;

        public ArrayProperty(long startOffset, List<T> values, ArrayType type, NameReference name) : base(name)
        {
            Offset = startOffset;
            PropType = PropertyType.ArrayProperty;
            arrayType = type;
            Values = values;
        }

        #region IEnumerable<T>
        public IEnumerator<T> GetEnumerator()
        {
            return Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Values.GetEnumerator();
        }
        #endregion

        #region IList<T>
        public int Count { get { return Values.Count; } }
        public bool IsReadOnly { get { return ((ICollection<T>)Values).IsReadOnly; } }

        public T this[int index]
        {
            get { return Values[index]; }
            set { Values[index] = value; }
        }

        public void Add(T item)
        {
            Values.Add(item);
        }

        public void Clear()
        {
            Values.Clear();
        }

        public bool Contains(T item)
        {
            return Values.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            Values.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            return Values.Remove(item);
        }

        public void RemoveAt(int index)
        {
            Values.RemoveAt(index);
        }

        public int IndexOf(T item)
        {
            return Values.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            Values.Insert(index, item);
        }
        #endregion
    }

    public class StrProperty : UProperty
    {
        string _value;
        public string Value
        {
            get { return _value; }
            set { _value = value; }
        }

        public StrProperty(MemoryStream stream, NameReference? name = null) : base(name)
        {
            Offset = stream.Position;
            int count = stream.ReadValueS32();
            var streamPos = stream.Position;

            if (count < -1) // originally 0
            {
                count *= -2;
                Value = stream.ReadString(count, true, Encoding.Unicode);
            }
            else if (count > 0)
            {
                Value = stream.ReadString(count, true, Encoding.ASCII);
            }
            else
            {
                Value = string.Empty;
                //ME3Explroer 3.0.2 and below wrote a null terminator character when writing an empty string.
                //The game however does not write an empty string if the length is 0 - it just happened to still work but not 100% of the time
                //This is for backwards compatibility with that as it will have a count of 0 instead of -1
                if (count == -1)
                {
                    stream.Position += 2;
                }
            }

            //for when the end of the string has multiple nulls at the end
            if (stream.Position < streamPos + count)
            {
                stream.Seek(streamPos + count, SeekOrigin.Begin);
            }

            PropType = PropertyType.StrProperty;
        }

        public StrProperty(string val, NameReference? name = null) : base(name)
        {
            Value = val ?? string.Empty;
            PropType = PropertyType.StrProperty;
        }

        public static implicit operator StrProperty(string s)
        {
            return new StrProperty(s);
        }

        public static implicit operator string(StrProperty p)
        {
            return p.Value;
        }

        public override string ToString()
        {
            return Value;
        }
    }

    public class StringRefProperty : UProperty
    {
        int _value;
        public int Value
        {
            get { return _value; }
            set { _value = value; }
        }

        public StringRefProperty(MemoryStream stream, NameReference? name = null) : base(name)
        {
            Offset = stream.Position;
            Value = stream.ReadValueS32();
            PropType = PropertyType.StringRefProperty;
        }
    }

    public class DelegateProperty : UProperty
    {
        public int unk;
        public NameReference Value;

        public DelegateProperty(MemoryStream stream, ME1Package pcc, NameReference? name = null) : base(name)
        {
            unk = stream.ReadValueS32();
            NameReference val = new NameReference();
            val.Name = pcc.getNameEntry(stream.ReadValueS32());
            val.Number = stream.ReadValueS32();
            Value = val;
            PropType = PropertyType.DelegateProperty;
        }
    }

    public class UnknownProperty : UProperty
    {
        public byte[] raw;
        public readonly string TypeName;

        public UnknownProperty(MemoryStream stream, int size, string typeName = null, NameReference? name = null) : base(name)
        {
            Offset = stream.Position;
            TypeName = typeName;
            raw = stream.ReadBytes(size);
            PropType = PropertyType.Unknown;
        }
    }

    public struct NameReference
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
    }
}

