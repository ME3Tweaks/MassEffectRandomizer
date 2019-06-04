﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static MassEffectRandomizer.Classes.ME1Package;

namespace MassEffectRandomizer.Classes
{
    public static class ME1UnrealObjectInfo
    {
        public static Dictionary<string, ClassInfo> Classes = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, ClassInfo> Structs = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, List<NameReference>> Enums = new Dictionary<string, List<NameReference>>();
        private static bool loaded;


        public static void loadfromJSON()
        {
            if (loaded)
            {
                return;
            }

            using (Stream stream = Utilities.GetResourceStream("MassEffectRandomizer.staticfiles.ME1ObjectInfo.json"))
            {
                TextReader tr = new StreamReader(stream);
                string raw = tr.ReadToEnd();
                var blob = JsonConvert.DeserializeAnonymousType(raw, new { Classes, Structs, Enums });
                Classes = blob.Classes;
                Structs = blob.Structs;
                Enums = blob.Enums;
                loaded = true;
            }
        }

        private static readonly string[] ImmutableStructs = { "Vector", "Color", "LinearColor", "TwoVectors", "Vector4", "Vector2D", "Rotator", "Guid", "Plane", "Box",
            "Quat", "Matrix", "IntPoint", "ActorReference", "PolyReference", "AimTransform","BioRwBox", "BioMask4Property", "RwVector2", "RwVector3", "RwVector4",
            "BioRwBox44" };

        public static bool isImmutableStruct(string structName)
        {
            return ImmutableStructs.Contains(structName);
        }

        public static string getEnumTypefromProp(string className, string propName, bool inStruct = false, ClassInfo nonVanillaClassInfo = null)
        {
            PropertyInfo p = getPropertyInfo(className, propName, inStruct, nonVanillaClassInfo);
            if (p == null && !inStruct)
            {
                p = getPropertyInfo(className, propName, true, nonVanillaClassInfo);
            }
            return p?.reference;
        }

        public static List<NameReference> getEnumfromProp(string className, string propName, bool inStruct = false)
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
                            List<NameReference> vals = getEnumfromProp(p.reference, propName, true);
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
                    List<NameReference> vals = getEnumfromProp(info.baseClass, propName, inStruct);
                    if (vals != null)
                    {
                        return vals;
                    }
                }
            }
            return null;
        }

        public static List<NameReference> GetEnumValues(string enumName, bool includeNone = false)
        {
            if (Enums.ContainsKey(enumName))
            {
                var values = new List<NameReference>(Enums[enumName]);
                if (includeNone)
                {
                    values.Insert(0, "None");
                }
                return values;
            }
            return null;
        }

        public static ArrayType getArrayType(string className, string propName, IExportEntry export = null)
        {
            PropertyInfo p = getPropertyInfo(className, propName, false, containingExport: export)
                          ?? getPropertyInfo(className, propName, true, containingExport: export);
            if (p == null && export != null)
            {
                if (export.ClassName != "Class" && export.idxClass > 0)
                {
                    export = export.FileRef.Exports[export.idxClass - 1]; //make sure you get actual class
                }
                if (export.ClassName == "Class")
                {
                    ClassInfo currentInfo = generateClassInfo(export);
                    currentInfo.baseClass = export.ClassParent;
                    p = getPropertyInfo(className, propName, false, currentInfo, containingExport: export)
                     ?? getPropertyInfo(className, propName, true, currentInfo, containingExport: export);
                }
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

        /// <summary>
        /// Wrapper that will call getPropertyInfoInternal and swap the in-struct search if the firstl ookup fails.
        /// </summary>
        /// <param name="className"></param>
        /// <param name="propName"></param>
        /// <param name="inStruct"></param>
        /// <param name="nonVanillaClassInfo"></param>
        /// <param name="reSearch"></param>
        /// <param name="containingExport"></param>
        /// <returns></returns>
        public static PropertyInfo getPropertyInfo(string className, string propName, bool inStruct = false, ClassInfo nonVanillaClassInfo = null, bool reSearch = true, IExportEntry containingExport = null)
        {
            var pinfo = getPropertyInfoInternal(className, propName, inStruct, nonVanillaClassInfo, reSearch, containingExport);
            if (pinfo == null)
            {
                pinfo = getPropertyInfoInternal(className, propName, !inStruct, nonVanillaClassInfo, reSearch, containingExport);
            }
            return pinfo;
        }

        private static PropertyInfo getPropertyInfoInternal(string className, string propName, bool inStruct = false, ClassInfo nonVanillaClassInfo = null, bool reSearch = true, IExportEntry containingExport = null)
        {
            if (className.StartsWith("Default__"))
            {
                className = className.Substring(9);
            }
            Dictionary<string, ClassInfo> temp = inStruct ? Structs : Classes;
            bool infoExists = temp.TryGetValue(className, out ClassInfo info);
            if (!infoExists && nonVanillaClassInfo != null)
            {
                info = nonVanillaClassInfo;
                infoExists = true;
            }
            if (infoExists) //|| (temp = !inStruct ? Structs : Classes).ContainsKey(className))
            {
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
                        if ((p.type == PropertyType.StructProperty || p.type == PropertyType.ArrayProperty) && reSearch)
                        {
                            PropertyInfo val = getPropertyInfo(p.reference, propName, true, nonVanillaClassInfo, reSearch: false);
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
                    PropertyInfo val = getPropertyInfo(info.baseClass, propName, inStruct, nonVanillaClassInfo, reSearch: true);
                    if (val != null)
                    {
                        return val;
                    }
                }
                else
                {
                    //Baseclass may be modified as well...
                    if (containingExport != null && containingExport.idxClassParent > 0)
                    {
                        //Class parent is in this file. Generate class parent info and attempt refetch
                        IExportEntry parentExport = containingExport.FileRef.getUExport(containingExport.idxClassParent);
                        return getPropertyInfo(parentExport.ClassParent, propName, inStruct, generateClassInfo(parentExport), reSearch: true, parentExport);
                    }
                }
            }

            //if (reSearch)
            //{
            //    PropertyInfo reAttempt = getPropertyInfo(className, propName, !inStruct, nonVanillaClassInfo, reSearch: false);
            //    return reAttempt; //will be null if not found.
            //}
            return null;
        }

        #region struct default values

        //COPIED FROM ME2. REQUIRES A REBUILD
        private readonly static byte[] CoverReferenceDefault = { 
            // >> CoverReference
            //CoverReference: Direction
            0x1F, 0x19, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0xA1, 0x2E, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,

            0x18, 0x49, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0xA1, 0x2E, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x16, 0x38, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x76, 0x3A, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x72, 0x2B, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,

            0x41, 0x4C, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x10, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x72, 0x2B, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, //A
            0x00, 0x00, 0x00, 0x00, //B
            0x00, 0x00, 0x00, 0x00, //C
            0x00, 0x00, 0x00, 0x00, //D

            //NONE
            0xC5, 0x39, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00};

        /*
         * Direction nameIdx 6431 0x191F INTPROP
         * 1F19000000000000A12E000000000000040000000000000000000000
         * SlotIdx nameIdx 18712 0x4918 INTPROP
         * 1849000000000000A12E000000000000040000000000000000000000
         * 
         * NAV REFERENCE BINARY
         * 1638000000000000763A000000000000040000000000000000000000722B000000000000414C0000000000001000000000000000722B00000000000000000000000000000000000000000000C539000000000000
         * 
         * 
         * 
         * 

        /*
            //SlotIdx
            0x78, 0x45, 0, 0, 0, 0, 0, 0, 0xB6, 0x29, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //Direction
            0x28, 0x1B, 0, 0, 0, 0, 0, 0, 0xB6, 0x29, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //Guid
            0xC7, 0x26, 0, 0, 0, 0, 0, 0, 0x17, 0x48, 0, 0, 0, 0, 0, 0, 0x10, 0, 0, 0, 0, 0, 0, 0, 0xC7, 0x26, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //Actor
            0xF7, 0, 0, 0, 0, 0, 0, 0, 0x62, 0x34, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
*/

        private static byte[] PlaneDefault = { 
            //TODO: THIS IS COPIED FROM ME3. REBUILD BYTES AS THEY WOULD APPEAR IN THE ENGINE.PCC WITH CORRECT NAME INDICES
            //X
            0x09, 0x03, 0, 0, 0, 0, 0, 0, 0x15, 0x01, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //Y
            0x0D, 0x03, 0, 0, 0, 0, 0, 0, 0x15, 0x01, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //Z
            0x12, 0x03, 0, 0, 0, 0, 0, 0, 0x15, 0x01, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //W
            0x04, 0x03, 0, 0, 0, 0, 0, 0, 0x15, 0x01, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //None
            0xF0, 0x01, 0, 0, 0, 0, 0, 0 };
        #endregion

        public static PropertyCollection getDefaultStructValue(string className, bool stripTransients = true)
        {
            if (Structs.ContainsKey(className))
            {
                bool immutable = isImmutableStruct(className);
                ClassInfo info = Structs[className];
                try
                {
                    if (info.pccPath != "ME3Explorer_CustomNativeAdditions")
                    {
                        string filepath = (Path.Combine(Utilities.GetGamePath(), "BioGame", info.pccPath));
                        if (File.Exists(info.pccPath))
                        {
                            filepath = info.pccPath; //Used for dynamic lookup
                        }

                        PropertyCollection props;
                        ME1Package importPCC = new ME1Package(filepath);
                        byte[] buff;
                        //Plane and CoverReference inherit from other structs, meaning they don't have default values (who knows why)
                        //thus, I have hardcoded what those default values should be 
                        if (className == "Plane")
                        {
                            buff = PlaneDefault;
                        }
                        else if (className == "CoverReference")
                        {
                            buff = CoverReferenceDefault;
                        }
                        else
                        {
                            var exportToRead = importPCC.Exports[info.exportIndex];
                            buff = exportToRead.Data.Skip(0x30).ToArray();
                        }
                        props = PropertyCollection.ReadProps(importPCC, new MemoryStream(buff), className);
                        if (stripTransients)
                        {
                            var toRemove = new List<UProperty>();
                            foreach (var prop in props)
                            {
                                //remove transient props
                                if (info.properties.TryGetValue(prop.Name, out PropertyInfo propInfo))
                                {
                                    if (propInfo.transient)
                                    {
                                        toRemove.Add(prop);
                                    }
                                }
                                //if (!info.properties.ContainsKey(prop.Name) && info.baseClass == "Class")
                                //{
                                //    toRemove.Add(prop);
                                //}
                            }
                            foreach (var prop in toRemove)
                            {
                                Debug.WriteLine($"ME1: Get Default Struct value ({className}) - removing transient prop: {prop.Name}");
                                props.Remove(prop);
                            }
                        }
                        return props;
                    }
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        internal static ClassInfo generateClassInfo(IExportEntry export)
        {
            return generateClassInfo(export.Index, export.FileRef as ME1Package);
        }

        private static ClassInfo generateClassInfo(int index, ME1Package pcc)
        {
            ClassInfo info = new ClassInfo
            {
                baseClass = pcc.Exports[index].ClassParent,
                exportIndex = index
            };
            if (pcc.FileName.Contains("BioGame"))
            {
                info.pccPath = new string(pcc.FileName.Skip(pcc.FileName.LastIndexOf("BioGame") + 8).ToArray());
            }
            else
            {
                info.pccPath = pcc.FileName; //used for dynamic resolution of files outside the game directory.
            }

            foreach (IExportEntry entry in pcc.Exports)
            {
                if (entry.idxLink - 1 == index && entry.ClassName != "ScriptStruct" && entry.ClassName != "Enum"
                    && entry.ClassName != "Function" && entry.ClassName != "Const" && entry.ClassName != "State")
                {
                    //Skip if property is transient (only used during execution, will never be in game files)
                    if (/*(BitConverter.ToUInt64(entry.Data, 24) & 0x0000000000002000) == 0 &&*/ !info.properties.ContainsKey(entry.ObjectName))
                    {
                        PropertyInfo p = getProperty(pcc, entry);
                        if (p != null)
                        {
                            info.properties.Add(entry.ObjectName, p);
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Skipping property due to flag: " + entry.ObjectName);
                    }
                }
            }
            return info;
        }
        private static PropertyInfo getProperty(ME1Package pcc, IExportEntry entry)
        {
            PropertyInfo p = new PropertyInfo();
            switch (entry.ClassName)
            {
                case "IntProperty":
                    p.type = PropertyType.IntProperty;
                    break;
                case "StringRefProperty":
                    p.type = PropertyType.StringRefProperty;
                    break;
                case "FloatProperty":
                    p.type = PropertyType.FloatProperty;
                    break;
                case "BoolProperty":
                    p.type = PropertyType.BoolProperty;
                    break;
                case "StrProperty":
                    p.type = PropertyType.StrProperty;
                    break;
                case "NameProperty":
                    p.type = PropertyType.NameProperty;
                    break;
                case "DelegateProperty":
                    p.type = PropertyType.DelegateProperty;
                    break;
                case "ObjectProperty":
                case "ClassProperty":
                case "ComponentProperty":
                    p.type = PropertyType.ObjectProperty;
                    p.reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "StructProperty":
                    p.type = PropertyType.StructProperty;
                    p.reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "BioMask4Property":
                case "ByteProperty":
                    p.type = PropertyType.ByteProperty;
                    p.reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "ArrayProperty":
                    p.type = PropertyType.ArrayProperty;
                    PropertyInfo arrayTypeProp = getProperty(pcc, pcc.Exports[BitConverter.ToInt32(entry.Data, 44) - 1]);
                    if (arrayTypeProp != null)
                    {
                        switch (arrayTypeProp.type)
                        {
                            case PropertyType.ObjectProperty:
                            case PropertyType.StructProperty:
                            case PropertyType.ArrayProperty:
                                p.reference = arrayTypeProp.reference;
                                break;
                            case PropertyType.ByteProperty:
                                if (arrayTypeProp.reference == "Class")
                                    p.reference = arrayTypeProp.type.ToString();
                                else
                                    p.reference = arrayTypeProp.reference;
                                break;
                            case PropertyType.IntProperty:
                            case PropertyType.FloatProperty:
                            case PropertyType.NameProperty:
                            case PropertyType.BoolProperty:
                            case PropertyType.StrProperty:
                            case PropertyType.StringRefProperty:
                            case PropertyType.DelegateProperty:
                                p.reference = arrayTypeProp.type.ToString();
                                break;
                            case PropertyType.None:
                            case PropertyType.Unknown:
                            default:
                                System.Diagnostics.Debugger.Break();
                                p = null;
                                break;
                        }
                    }
                    else
                    {
                        p = null;
                    }
                    break;
                case "InterfaceProperty":
                default:
                    p = null;
                    break;
            }
            if (p != null && (BitConverter.ToUInt64(entry.Data, 24) & 0x0000000000002000) != 0)
            {
                //Transient
                p.transient = true;
            }
            return p;
        }
    }
    public class ClassInfo
    {
        public Dictionary<string, PropertyInfo> properties = new Dictionary<string, PropertyInfo>();
        public string baseClass;
        //Relative to BIOGame
        public string pccPath;
        //0-based
        public int exportIndex;
    }

    [DebuggerDisplay("PropertyInfo | {type} , parent: {reference}, transient: {transient}")]
    public class PropertyInfo
    {
        public PropertyType type { get; set; }
        public string reference;
        public bool transient;

        public bool IsEnumProp() => type == PropertyType.ByteProperty && reference != null && reference != "Class" && reference != "Object";
    }

    public enum ArrayType
    {
        Object,
        Name,
        Enum,
        Struct,
        Bool,
        String,
        Float,
        Int,
        Byte,
    }
}
