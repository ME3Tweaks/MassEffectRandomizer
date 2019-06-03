﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectRandomizer.Classes
{
    [DebuggerDisplay("ImportEntry | {UIndex} = {GetFullPath}")]
    public class ImportEntry : IEntry
    {
        public ImportEntry(ME1Package pccFile, Stream importData)
        {
            HeaderOffset = importData.Position;
            FileRef = pccFile;
            Header = new byte[byteSize];
            importData.Read(Header, 0, Header.Length);
        }

        public ImportEntry(ME1Package pccFile)
        {
            FileRef = pccFile;
            Header = new byte[byteSize];
        }

        public long HeaderOffset { get; set; }

        public int Index { get; set; }
        public int UIndex => -Index - 1;

        public ME1Package FileRef { get; protected set; }

        public const int byteSize = 28;

        protected byte[] _header;
        public byte[] Header
        {
            get => _header;
            set
            {
                bool isFirstLoad = _header == null;
                if (_header != null && value != null && _header.SequenceEqual(value))
                {
                    return; //if the data is the same don't write it and trigger the side effects
                }
                _header = value;
                if (!isFirstLoad)
                {
                    HeaderChanged = true;
                    EntryHasPendingChanges = true;
                }
            }
        }

        /// <summary>
        /// Returns a clone of the header for modifying
        /// </summary>
        /// <returns></returns>
        public byte[] GetHeader()
        {
            return _header.TypedClone();
        }

        public bool HasParent => FileRef.isEntry(idxLink);

        public IEntry Parent
        {
            get => FileRef.getEntry(idxLink);
            set => idxLink = value.UIndex;
        }

        public int idxPackageFile
        {
            get => BitConverter.ToInt32(Header, 0);
            set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 0, sizeof(int)); HeaderChanged = true; }
        }
        //int PackageNameNumber
        public int idxClassName
        {
            get => BitConverter.ToInt32(Header, 8);
            set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 8, sizeof(int)); HeaderChanged = true; }
        }
        //int ClassNameNumber
        public int idxLink
        {
            get => BitConverter.ToInt32(Header, 16);
            set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 16, sizeof(int)); HeaderChanged = true; }
        }
        public int idxObjectName
        {
            get => BitConverter.ToInt32(Header, 20);
            set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 20, sizeof(int)); HeaderChanged = true; }
        }
        public int indexValue
        {
            get => BitConverter.ToInt32(Header, 24);
            set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 24, sizeof(int)); HeaderChanged = true; }
        }




        public string ClassName => FileRef.Names[idxClassName];
        public string PackageFile => FileRef.Names[idxPackageFile] + ".pcc"; //Is this valid for ME1?
        public string ObjectName => FileRef.Names[idxObjectName];
        public string PackageFileNoExtension { get { return FileRef.Names[idxPackageFile]; } }


        public string PackageName
        {
            get
            {
                int val = idxLink;
                if (val != 0)
                {
                    IEntry entry = FileRef.getEntry(val);
                    return FileRef.Names[entry.idxObjectName];
                }
                else return "Package";
            }
        }

        public string PackageNameInstanced
        {
            get
            {
                int val = idxLink;
                if (val != 0)
                {
                    IEntry entry = FileRef.getEntry(val);
                    string result = FileRef.Names[entry.idxObjectName];
                    if (entry.indexValue > 0)
                    {
                        return result + "_" + entry.indexValue; //Should be -1 for 4.1, will remain as-is for 4.0
                    }
                    return result;
                }
                else return "Package";
            }
        }

        public string PackageFullName
        {
            get
            {
                string result = PackageName;
                int idxNewPackName = idxLink;

                while (idxNewPackName != 0)
                {
                    string newPackageName = FileRef.getEntry(idxNewPackName).PackageName;
                    if (newPackageName != "Package")
                        result = newPackageName + "." + result;
                    idxNewPackName = FileRef.getEntry(idxNewPackName).idxLink;
                }
                return result;
            }
        }

        public string GetFullPath
        {
            get
            {
                string s = "";
                if (PackageFullName != "Class" && PackageFullName != "Package")
                    s += PackageFullName + ".";
                s += ObjectName;
                return s;
            }
        }
        public string GetIndexedFullPath
        {
            get
            {
                return GetFullPath + "_" + indexValue;
            }
        }

        public string PackageFullNameInstanced
        {
            get
            {
                string result = PackageName;
                int idxNewPackName = idxLink;

                while (idxNewPackName != 0)
                {
                    IEntry e = FileRef.getEntry(idxNewPackName);
                    string newPackageName = e.PackageName;
                    if (e.indexValue > 0)
                    {
                        newPackageName += "_" + e.indexValue;
                    }
                    if (newPackageName != "Package")
                        result = newPackageName + "." + result;
                    idxNewPackName = FileRef.getEntry(idxNewPackName).idxLink;
                }
                return result;
            }
        }

        public string GetInstancedFullPath
        {
            get
            {
                string s = "";
                if (PackageFullNameInstanced != "Class" && PackageFullNameInstanced != "Package")
                    s += PackageFullNameInstanced + ".";
                s += ObjectName;
                s += "_" + indexValue;
                return s;
            }
        }

        bool headerChanged;
        public bool HeaderChanged
        {
            get => headerChanged;

            set
            {
                headerChanged = value;
            }
        }


        private bool _entryHasPendingChanges = false;
        private IEntry _entryImplementation;

        public bool EntryHasPendingChanges
        {
            get => _entryHasPendingChanges;
            set
            {
                if (value != _entryHasPendingChanges)
                {
                    _entryHasPendingChanges = value;
                }
            }
        }

        public ImportEntry Clone()
        {
            ImportEntry newImport = (ImportEntry)MemberwiseClone();
            newImport.Header = (byte[])Header.Clone();
            return newImport;
        }
    }
}
