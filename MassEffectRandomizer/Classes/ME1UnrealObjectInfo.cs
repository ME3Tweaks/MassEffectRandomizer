using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static MassEffectRandomizer.Classes.ME1Package;

namespace MassEffectRandomizer.Classes
{
    public static class ME1UnrealObjectInfo
    {
        public static Dictionary<string, ClassInfo> Classes = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, ClassInfo> Structs = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, List<string>> Enums = new Dictionary<string, List<string>>();


        private static bool loaded;

        public static void loadfromJSON()
        {
            if (loaded)
            {
                return;
            }

            using (Stream stream = Utilities.GetResourceStream("MassEffectRandomizer.staticfiles.ME1ObjectInfo.json")) {
                TextReader tr = new StreamReader(stream);
                string raw = tr.ReadToEnd();
                var blob = JsonConvert.DeserializeAnonymousType(raw, new { Classes, Structs, Enums });
                Classes = blob.Classes;
                Structs = blob.Structs;
                Enums = blob.Enums;
                loaded = true;
            }
        }

        public static string getEnumTypefromProp(string className, string propName, bool inStruct = false)
        {
            PropertyInfo p = getPropertyInfo(className, propName, inStruct);
            return p?.reference;
        }

        private static string[] ImmutableStructs = { "Vector", "Color", "LinearColor", "TwoVectors", "Vector4", "Vector2D", "Rotator", "Guid", "Plane", "Box",
            "Quat", "Matrix", "IntPoint", "ActorReference", "ActorReference", "ActorReference", "PolyReference", "AimTransform", "AimTransform", "NavReference",
            "CoverReference", "CoverInfo", "CoverSlot", "BioRwBox", "BioMask4Property", "RwVector2", "RwVector3", "RwVector4", "BioRwBox44" };

        public static bool isImmutable(string structName)
        {
            return ImmutableStructs.Contains(structName);
        }

        //public static byte[] getDefaultClassValue(PCCPackage pcc, string className, bool fullProps = false)
        //{
        //    if (Structs.ContainsKey(className))
        //    {
        //        bool isImmutable = ImmutableStructs.Contains(className);
        //        ClassInfo info = Structs[className];
        //        PCCPackage importPCC = new PCCPackage(Path.Combine(ME3Directory.gamePath, @"BIOGame\" + info.pccPath));
        //        byte[] buff;
        //        //Plane and CoverReference inherit from other structs, meaning they don't have default values (who knows why)
        //        //thus, I have hardcoded what those default values should be 
        //        if (className == "Plane")
        //        {
        //            buff = PlaneDefault;
        //        }
        //        else if (className == "CoverReference")
        //        {
        //            buff = CoverReferenceDefault;
        //        }
        //        else
        //        {
        //            buff = importPCC.Exports[info.exportIndex].Data.Skip(0x24).ToArray();
        //        }
        //        List<PropertyReader.Property> Props = PropertyReader.ReadProp(importPCC, buff, 0);
        //        MemoryStream m = new MemoryStream();
        //        foreach (PropertyReader.Property p in Props)
        //        {
        //            string propName = importPCC.GetName(p.Name);
        //            //check if property is transient, if so, skip (neither of the structs that inherit have transient props)
        //            if (info.properties.ContainsKey(propName) || propName == "None" || info.baseClass != "Class")
        //            {
        //                if (isImmutable && !fullProps)
        //                {
        //                    PropertyReader.ImportImmutableProperty(pcc, importPCC, p, className, m, true);
        //                }
        //                else
        //                {
        //                    PropertyReader.ImportProperty(pcc, importPCC, p, className, m, true);
        //                }
        //            }
        //        }
        //        importPCC.Source.Close();
        //        return m.ToArray();
        //    }
        //    else if (Classes.ContainsKey(className))
        //    {
        //        ClassInfo info = Structs[className];
        //        PCCPackage importPCC = new PCCPackage(Path.Combine(ME3Directory.gamePath, @"BIOGame\" + info.pccPath));
        //        PCCPackage.ExportEntry entry = pcc.Exports[info.exportIndex + 1];
        //        List<PropertyReader.Property> Props = PropertyReader.getPropList(importPCC, entry);
        //        MemoryStream m = new MemoryStream(entry.Datasize - 4);
        //        foreach (PropertyReader.Property p in Props)
        //        {
        //            if (!info.properties.ContainsKey(importPCC.GetName(p.Name)))
        //            {
        //                //property is transient
        //                continue;
        //            }
        //            PropertyReader.ImportProperty(pcc, importPCC, p, className, m);
        //        }
        //        return m.ToArray();
        //    }
        //    return null;
        //}

        public static List<string> getEnumfromProp(string className, string propName, bool inStruct = false)
        {
            Dictionary<string, ClassInfo> temp = inStruct ? Structs : Classes;
            if (temp.ContainsKey(className))
            {
                ClassInfo info = temp[className];
                //look in class properties
                if (info.properties.ContainsKey(propName))
                {
                    PropertyInfo p = info.properties[propName];
                    if (Enums.ContainsKey(p.reference))
                    {
                        return Enums[p.reference];
                    }
                }
                //look in structs
                else
                {
                    foreach (PropertyInfo p in info.properties.Values)
                    {
                        if (p.type == PropertyType.StructProperty || p.type == PropertyType.ArrayProperty)
                        {
                            List<string> vals = getEnumfromProp(p.reference, propName, true);
                            if (vals != null)
                            {
                                return vals;
                            }
                        }
                    }
                }
                //look in base class
                if (temp.ContainsKey(info.baseClass))
                {
                    List<string> vals = getEnumfromProp(info.baseClass, propName, inStruct);
                    if (vals != null)
                    {
                        return vals;
                    }
                }
            }
            return null;
        }

        public static List<string> GetEnumValues(string enumName, bool includeNone = false)
        {
            if (Enums.ContainsKey(enumName))
            {
                List<string> values = new List<string>(Enums[enumName]);
                if (includeNone)
                {
                    values.Insert(0, "None");
                }
                return values;
            }
            return null;
        }

        public static ArrayType getArrayType(string className, string propName, bool inStruct = false)
        {
            PropertyInfo p = getPropertyInfo(className, propName, inStruct);
            if (p == null)
            {
                p = getPropertyInfo(className, propName, !inStruct);
            }
            return getArrayType(p);
        }

        public static ArrayType getArrayType(PropertyInfo p)
        {
            if (p != null)
            {
                if (p.reference == "NameProperty")
                {
                    return ArrayType.Name;
                }
                else if (Enums.ContainsKey(p.reference))
                {
                    return ArrayType.Enum;
                }
                else if (p.reference == "BoolProperty")
                {
                    return ArrayType.Bool;
                }
                else if (p.reference == "ByteProperty")
                {
                    return ArrayType.Byte;
                }
                else if (p.reference == "StrProperty")
                {
                    return ArrayType.String;
                }
                else if (p.reference == "FloatProperty")
                {
                    return ArrayType.Float;
                }
                else if (p.reference == "IntProperty")
                {
                    return ArrayType.Int;
                }
                else if (Structs.ContainsKey(p.reference))
                {
                    return ArrayType.Struct;
                }
                else
                {
                    return ArrayType.Object;
                }
            }
            else
            {
                return ArrayType.Int;
            }
        }

        public static PropertyInfo getPropertyInfo(string className, string propName, bool inStruct = false)
        {
            if (className.StartsWith("Default__"))
            {
                className = className.Substring(9);
            }
            Dictionary<string, ClassInfo> temp = inStruct ? Structs : Classes;
            if (temp.ContainsKey(className)) //|| (temp = !inStruct ? Structs : Classes).ContainsKey(className))
            {
                ClassInfo info = temp[className];
                //look in class properties
                if (info.properties.ContainsKey(propName))
                {
                    return info.properties[propName];
                }
                //look in structs
                else
                {
                    foreach (PropertyInfo p in info.properties.Values)
                    {
                        if (p.type == PropertyType.StructProperty || p.type == PropertyType.ArrayProperty)
                        {
                            PropertyInfo val = getPropertyInfo(p.reference, propName, true);
                            if (val != null)
                            {
                                return val;
                            }
                        }
                    }
                }
                //look in base class
                if (temp.ContainsKey(info.baseClass))
                {
                    PropertyInfo val = getPropertyInfo(info.baseClass, propName, inStruct);
                    if (val != null)
                    {
                        return val;
                    }
                }
            }
            return null;
        }

        public static bool inheritsFrom(ME1ExportEntry entry, string baseClass)
        {
            string className = entry.ClassName;
            while (Classes.ContainsKey(className))
            {
                if (className == baseClass)
                {
                    return true;
                }
                className = Classes[className].baseClass;
            }
            return false;
        }
    }
}
