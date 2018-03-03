using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectRandomizer.Classes
{
    public abstract class MEPackage
    {
        protected const int appendFlag = 0x00100000;

        public string FileName { get; protected set; }

        public bool IsModified
        {
            get
            {
                return exports.Any(entry => entry.DataChanged == true) || imports.Any(entry => entry.HeaderChanged == true) || namesAdded > 0;
            }
        }
        public bool CanReconstruct { get { return !exports.Exists(x => x.ObjectName == "SeekFreeShaderCache" && x.ClassName == "ShaderCache"); } }

        protected byte[] header;
        protected uint magic { get { return BitConverter.ToUInt32(header, 0); } }
        protected ushort lowVers { get { return BitConverter.ToUInt16(header, 4); } }
        protected ushort highVers { get { return BitConverter.ToUInt16(header, 6); } }
        protected int expDataBegOffset { get { return BitConverter.ToInt32(header, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int)); } }
        protected int nameSize { get { int val = BitConverter.ToInt32(header, 12); return (val < 0) ? val * -2 : val; } }
        protected uint flags { get { return BitConverter.ToUInt32(header, 16 + nameSize); } }


        public abstract int NameCount { get; protected set; }
        public abstract int ImportCount { get; protected set; }
        public abstract int ExportCount { get; protected set; }

        public bool IsCompressed
        {
            get { return (flags & 0x02000000) != 0; }
            protected set
            {
                if (value) // sets the compressed flag if bCompressed set equal to true
                    Buffer.BlockCopy(BitConverter.GetBytes(flags | 0x02000000), 0, header, 16 + nameSize, sizeof(int));
                else // else set to false
                    Buffer.BlockCopy(BitConverter.GetBytes(flags & ~0x02000000), 0, header, 16 + nameSize, sizeof(int));
            }
        }
        //has been saved with the revised Append method
        public bool IsAppend
        {
            get { return (flags & appendFlag) != 0; }
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
        public IReadOnlyList<string> Names { get { return names; } }

        public bool isName(int index)
        {
            return (index >= 0 && index < names.Count);
        }

        public string getNameEntry(int index)
        {
            if (!isName(index))
                return "";
            return names[index];
        }

        public int FindNameOrAdd(string name)
        {
            for (int i = 0; i < names.Count; i++)
                if (names[i] == name)
                    return i;
            addName(name);
            return names.Count - 1;
        }

        public void addName(string name)
        {
            if (!names.Contains(name))
            {
                names.Add(name);
                namesAdded++;
                NameCount = names.Count;
            }
        }

        public void replaceName(int idx, string newName)
        {
            if (idx >= 0 && idx <= names.Count - 1)
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

        public void setNames(List<string> list)
        {
            names = list;
        }
        #endregion

        #region Exports
        protected List<IExportEntry> exports;
        public IReadOnlyList<IExportEntry> Exports
        {
            get
            {
                return exports;
            }
        }

        public bool isExport(int index)
        {
            return (index >= 0 && index < exports.Count);
        }

        public void addExport(IExportEntry exportEntry)
        {
            if (exportEntry.FileRef != this)
                throw new Exception("you cannot add a new export entry from another pcc file, it has invalid references!");

            exportEntry.DataChanged = true;
            exportEntry.Index = exports.Count;
            exports.Add(exportEntry);
            ExportCount = exports.Count;
        }

        public IExportEntry getExport(int index)
        {
            return exports[index];
        }
        #endregion

        #region Imports
        protected List<ImportEntry> imports;
        public IReadOnlyList<ImportEntry> Imports
        {
            get
            {
                return imports;
            }
        }

        public bool isImport(int index)
        {
            return (index >= 0 && index < imports.Count);
        }

        public void addImport(ImportEntry importEntry)
        {
            if (importEntry.FileRef != this)
                throw new Exception("you cannot add a new import entry from another pcc file, it has invalid references!");

            importEntry.Index = imports.Count;
            imports.Add(importEntry);
            ImportCount = imports.Count;
        }

        public ImportEntry getImport(int index)
        {
            return imports[index];
        }

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
        /// <param name="index">unreal index</param>
        public IEntry getEntry(int index)
        {
            if (index > 0 && index <= ExportCount)
                return exports[index - 1];
            if (-index > 0 && -index <= ImportCount)
                return imports[-index - 1];
            return null;
        }
        #endregion

        private DateTime? lastSaved;
        public DateTime LastSaved
        {
            get
            {
                if (lastSaved.HasValue)
                {
                    return lastSaved.Value;
                }
                else if (File.Exists(FileName))
                {
                    return (new FileInfo(FileName)).LastWriteTime;
                }
                else
                {
                    return DateTime.MinValue;
                }
            }
        }

        public long FileSize
        {
            get
            {
                if (File.Exists(FileName))
                {
                    return (new FileInfo(FileName)).Length;
                }
                return 0;
            }
        }

        protected virtual void AfterSave()
        {
            foreach (var export in exports)
            {
                export.DataChanged = false;
            }
            foreach (var import in imports)
            {
                import.HeaderChanged = false;
            }
            namesAdded = 0;

        }
    }
}
