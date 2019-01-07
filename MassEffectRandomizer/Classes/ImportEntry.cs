using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectRandomizer.Classes
{
    public class ImportEntry : IEntry
    {
        public ImportEntry(ME1Package pccFile, Stream importData)
        {
            FileRef = pccFile;
            Header = new byte[byteSize];
            importData.Read(Header, 0, Header.Length);
        }

        public ImportEntry(ME1Package pccFile)
        {
            FileRef = pccFile;
            Header = new byte[byteSize];
        }

        public int Index { get; set; }
        public int UIndex { get { return -Index - 1; } }

        public ME1Package FileRef { get; protected set; }

        public const int byteSize = 28;
        public byte[] Header { get; protected set; }

        public int idxPackageName { get { return BitConverter.ToInt32(Header, 0); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 0, sizeof(int)); HeaderChanged = true; } }
        //int PackageNameNumber
        public int idxClassName { get { return BitConverter.ToInt32(Header, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 8, sizeof(int)); HeaderChanged = true; } }
        //int ClassNameNumber
        public int idxLink { get { return BitConverter.ToInt32(Header, 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 16, sizeof(int)); HeaderChanged = true; } }
        public int idxObjectName { get { return BitConverter.ToInt32(Header, 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Header, 20, sizeof(int)); HeaderChanged = true; } }
        //int ObjectNameNumber

        public string ClassName { get { return FileRef.Names[idxClassName]; } }
        public string PackageFile { get { return FileRef.Names[idxPackageName] + ".pcc"; } }
        public string ObjectName { get { return FileRef.Names[idxObjectName]; } }

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

        private bool _headerChanged;
        public bool HeaderChanged
        {
            get
            {
                return _headerChanged;
            }

            set
            {
                _headerChanged = value;
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
