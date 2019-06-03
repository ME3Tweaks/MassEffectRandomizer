﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectRandomizer.Classes
{
    public enum PackageChange
    {
        ExportData,
        ExportHeader,
        Import,
        Names,
        ExportAdd,
        ImportAdd,
    }

    [DebuggerDisplay("PackageUpdate | {change} on index {index}")]
    public struct PackageUpdate
    {
        /// <summary>
        /// Details on what piece of data has changed
        /// </summary>
        public PackageChange change;
        /// <summary>
        /// 0-based index of what item has changed in this package -1 = import 0, 0 = export 0
        /// </summary>
        public int index;
    }

    public abstract class MEPackage
    {
        protected const int appendFlag = 0x00100000;

        public string FileName { get; protected set; }

        public bool IsModified
        {
            get
            {
                return exports.Any(entry => entry.DataChanged || entry.HeaderChanged) || imports.Any(entry => entry.HeaderChanged) || namesAdded > 0;
            }
        }

        public string GetEntryString(int index)
        {
            if (index == 0)
            {
                return "Null";
            }
            string retStr = "Entry not found";
            IEntry coreRefEntry = getEntry(index);
            if (coreRefEntry != null)
            {
                retStr = coreRefEntry is ImportEntry ? "[I] " : "[E] ";
                retStr += coreRefEntry.GetIndexedFullPath;
            }
            return retStr;
        }
        public bool CanReconstruct { get { return !exports.Exists(x => x.ObjectName == "SeekFreeShaderCache" && x.ClassName == "ShaderCache"); } }

        protected byte[] header;
        protected uint magic => BitConverter.ToUInt32(header, 0);
        protected ushort lowVers => BitConverter.ToUInt16(header, 4);
        protected ushort highVers => BitConverter.ToUInt16(header, 6);
        protected int expDataBegOffset
        {
            get => BitConverter.ToInt32(header, 8);
            set => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int));
        }
        protected int nameSize { get { int val = BitConverter.ToInt32(header, 12); return (val < 0) ? val * -2 : val; } } //this may be able to be optimized. It is used a lot during package load
        protected uint flags => BitConverter.ToUInt32(header, 16 + nameSize);

        public abstract int NameCount { get; protected set; }
        public abstract int ImportCount { get; protected set; }
        public abstract int ExportCount { get; protected set; }

        public byte[] getHeader() { return header; }

        public bool IsCompressed
        {
            get => (flags & 0x02000000) != 0;
            protected set
            {
                if (value) // sets the compressed flag if bCompressed set equal to true
                {
                    //Toolkit never should never set this flag as we do not support compressing files.
                    Buffer.BlockCopy(BitConverter.GetBytes(flags | 0x02000000), 0, header, 16 + nameSize, sizeof(int));
                }
                else // else set to false
                {
                    Buffer.BlockCopy(BitConverter.GetBytes(flags & ~0x02000000), 0, header, 16 + nameSize, sizeof(int));
                    PackageCompressionType = CompressionType.None;
                }
            }
        }

        public enum CompressionType
        {
            None = 0,
            Zlib,
            LZO
        }

        public CompressionType PackageCompressionType
        {
            get => (CompressionType)BitConverter.ToInt32(header, header.Length - 4);
            set => header?.OverwriteRange(-4, BitConverter.GetBytes((int)value));
        }

        //has been saved with the revised Append method
        public bool IsAppend
        {
            get => (flags & appendFlag) != 0;
            protected set
            {
                if (value) // sets the append flag if IsAppend set equal to true
                    Buffer.BlockCopy(BitConverter.GetBytes(flags | appendFlag), 0, header, 16 + nameSize, sizeof(int));
                else // else set to false
                    Buffer.BlockCopy(BitConverter.GetBytes(flags & ~appendFlag), 0, header, 16 + nameSize, sizeof(int));
            }
        }

        #region Names
        protected uint namesAdded;
        protected List<string> names;
        public IReadOnlyList<string> Names => names;

        public bool isName(int index) => index >= 0 && index < names.Count;

        public string getNameEntry(int index) => isName(index) ? names[index] : "";

        public int FindNameOrAdd(string name)
        {
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i] == name)
                    return i;
            }

            addName(name);
            return names.Count - 1;
        }

        public void addName(string name)
        {
            if (name == null)
            {
                throw new Exception("Cannot add a null name to the list of names for a package file.\nThis is a bug in ME3Explorer.");
            }
            if (!names.Contains(name))
            {
                names.Add(name);
                namesAdded++;
                NameCount = names.Count;
            }
        }

        public void replaceName(int idx, string newName)
        {
            if (isName(idx))
            {
                names[idx] = newName;
            }
        }

        /// <summary>
        /// Checks whether a name exists in the PCC and returns its index
        /// If it doesn't exist returns -1
        /// </summary>
        /// <param name="nameToFind">The name of the string to find</param>
        /// <returns></returns>
        public int findName(string nameToFind)
        {
            for (int i = 0; i < names.Count; i++)
            {
                if (string.Compare(nameToFind, getNameEntry(i)) == 0)
                    return i;
            }
            return -1;
        }

        public void setNames(List<string> list) => names = list;

        #endregion

        #region Exports
        protected List<IExportEntry> exports;
        public IReadOnlyList<IExportEntry> Exports => exports;

        public bool isExport(int index) => index >= 0 && index < exports.Count;
        public bool isUExport(int uindex) => uindex > 0 && uindex <= exports.Count;

        public void addExport(IExportEntry exportEntry)
        {
            if (exportEntry.FileRef != this)
                throw new Exception("you cannot add a new export entry from another pcc file, it has invalid references!");

            exportEntry.DataChanged = true;
            exportEntry.Index = exports.Count;
            exports.Add(exportEntry);
            ExportCount = exports.Count;
        }

        public IExportEntry getExport(int index) => exports[index];
        public IExportEntry getUExport(int uindex) => exports[uindex - 1];

        #endregion

        #region Imports
        protected List<ImportEntry> imports;
        public IReadOnlyList<ImportEntry> Imports => imports;

        public bool isImport(int index) => (index >= 0 && index < ImportCount);
        public bool isUImport(int uindex) => (uindex < 0 && Math.Abs(uindex) <= ImportCount);

        public void addImport(ImportEntry importEntry)
        {
            if (importEntry.FileRef != this)
                throw new Exception("you cannot add a new import entry from another pcc file, it has invalid references!");

            importEntry.Index = imports.Count;
            imports.Add(importEntry);
            ImportCount = imports.Count;
        }

        public ImportEntry getImport(int index) => imports[index];
        public ImportEntry getUImport(int uindex) => imports[Math.Abs(uindex) - 1];

        #endregion

        #region IEntry
        /// <summary>
        ///     gets Export or Import name
        /// </summary>
        /// <param name="index">unreal index</param>
        public string getObjectName(int index)
        {
            if (index > 0 && index <= ExportCount)
                return exports[index - 1].ObjectName;
            if (-index > 0 && -index <= ImportCount)
                return imports[-index - 1].ObjectName;
            if (index == 0)
                return "Class";
            return "";
        }

        /// <summary>
        ///     gets Export or Import class
        /// </summary>
        /// <param name="index">unreal index</param>
        public string getObjectClass(int index)
        {
            if (index > 0 && index <= ExportCount)
                return exports[index - 1].ClassName;
            if (-index > 0 && -index <= ImportCount)
                return imports[-index - 1].ClassName;
            return "";
        }

        /// <summary>
        ///     gets Export or Import entry
        /// </summary>
        /// <param name="uindex">unreal index</param>
        public IEntry getEntry(int uindex)
        {
            if (uindex > 0 && uindex <= ExportCount)
                return exports[uindex - 1];
            if (-uindex > 0 && -uindex <= ImportCount)
                return imports[-uindex - 1];
            return null;
        }
        public bool isEntry(int uindex) => (uindex > 0 && uindex <= ExportCount) || (uindex != int.MinValue && Math.Abs(uindex) > 0 && Math.Abs(uindex) <= ImportCount);

        #endregion

        public string FollowLink(int Link)
        {
            string s = "";
            if (Link > 0 && isExport(Link - 1))
            {
                s = Exports[Link - 1].ObjectName + ".";
                s = FollowLink(Exports[Link - 1].idxLink) + s;
            }
            if (Link < 0 && isImport(Link * -1 - 1))
            {
                s = Imports[Link * -1 - 1].ObjectName + ".";
                s = FollowLink(Imports[Link * -1 - 1].idxLink) + s;
            }
            return s;
        }

        private DateTime? lastSaved;
        public DateTime LastSaved
        {
            get
            {
                if (lastSaved.HasValue)
                {
                    return lastSaved.Value;
                }

                if (File.Exists(FileName))
                {
                    return (new FileInfo(FileName)).LastWriteTime;
                }

                return DateTime.MinValue;
            }
        }

        public long FileSize => File.Exists(FileName) ? (new FileInfo(FileName)).Length : 0;

        protected virtual void AfterSave()
        {
            //We do if checks here to prevent firing tons of extra events as we can't prevent firing chanage notifications if 
            //it's not really a change due to the side effects of suppressing that.
            foreach (var export in exports)
            {
                if (export.DataChanged)
                {
                    export.DataChanged = false;
                }
                if (export.HeaderChanged)
                {
                    export.HeaderChanged = false;
                }
                if (export.EntryHasPendingChanges)
                {
                    export.EntryHasPendingChanges = false;
                }
            }
            foreach (var import in imports)
            {
                if (import.HeaderChanged)
                {
                    import.HeaderChanged = false;
                }
                if (import.EntryHasPendingChanges)
                {
                    import.EntryHasPendingChanges = false;
                }
            }
            namesAdded = 0;

            lastSaved = DateTime.Now;
        }
    }
}