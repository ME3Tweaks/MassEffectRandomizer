using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;     //This namespace is used to work with Registry editor.
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Xml;
using System.Windows;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.ComponentModel;
using System.Management;
using Serilog;

namespace MassEffectRandomizer.Classes
{
    public class Utilities
    {
        public const uint MEMI_TAG = 0x494D454D;

        public const int WIN32_EXCEPTION_ELEVATED_CODE = -98763;
        [DllImport("kernel32.dll")]
        static extern uint GetLastError();
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetPhysicallyInstalledSystemMemory(out long TotalMemoryInKilobytes);
        public static string GetOperatingSystemInfo()
        {
            StringBuilder sb = new StringBuilder();
            //Create an object of ManagementObjectSearcher class and pass query as parameter.
            ManagementObjectSearcher mos = new ManagementObjectSearcher("select * from Win32_OperatingSystem");
            foreach (ManagementObject managementObject in mos.Get())
            {
                if (managementObject["Caption"] != null)
                {
                    sb.AppendLine("Operating System Name  :  " + managementObject["Caption"].ToString());   //Display operating system caption
                }
                if (managementObject["OSArchitecture"] != null)
                {
                    sb.AppendLine("Operating System Architecture  :  " + managementObject["OSArchitecture"].ToString());   //Display operating system architecture.
                }
                if (managementObject["CSDVersion"] != null)
                {
                    sb.AppendLine("Operating System Service Pack   :  " + managementObject["CSDVersion"].ToString());     //Display operating system version.
                }
            }
            sb.AppendLine("\nProcessor Information-------");
            RegistryKey processor_name = Registry.LocalMachine.OpenSubKey(@"Hardware\Description\System\CentralProcessor\0", RegistryKeyPermissionCheck.ReadSubTree);   //This registry entry contains entry for processor info.

            if (processor_name != null)
            {
                if (processor_name.GetValue("ProcessorNameString") != null)
                {
                    sb.AppendLine((string)processor_name.GetValue("ProcessorNameString"));   //Display processor ingo.
                }
            }
            return sb.ToString();
        }

        internal static string GetAppDataFolder()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\MassEffectRandomizer";
        }

        internal static void Restore2DAFiles()
        {
            List<string> files = new List<string>();
            files.Add(Path.Combine("BioGame", "CookedPC", "Maps", "EntryMenu.SFM"));
            files.Add(Path.Combine("BioGame", "CookedPC", "Engine.u"));

            string gamepath = GetGamePath();
            string backuppath = Path.Combine(GetAppDataFolder(), "2dabackup");

            foreach (string file in files)
            {
                var sourcepath = Path.Combine(backuppath, file);
                var destpath = Path.Combine(gamepath, file);
                File.Copy(sourcepath, destpath, true);
            }
        }

        /// <summary> Checks for write access for the given file.
        /// </summary>
        /// <param name="fileName">The filename.</param>
        /// <returns>true, if write access is allowed, otherwise false</returns>
        public static bool IsDirectoryWritable(string dir)
        {
            var files = Directory.GetFiles(dir);
            string fileName = "";
            if (files.Count() > 0)
            {
                fileName = files[0];
            }
            else
            {
                try
                {
                    System.IO.File.Create(Path.Combine(dir, "temp.txt")).Close();
                    System.IO.File.Delete(Path.Combine(dir, "temp.txt"));
                    return true;
                }
                catch (System.UnauthorizedAccessException)
                {
                    return false;
                }
            }

            if ((File.GetAttributes(fileName) & FileAttributes.ReadOnly) != 0)
                return false;

            // Get the access rules of the specified files (user groups and user names that have access to the file)
            var rules = File.GetAccessControl(fileName).GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));

            // Get the identity of the current user and the groups that the user is in.
            var groups = WindowsIdentity.GetCurrent().Groups;
            string sidCurrentUser = WindowsIdentity.GetCurrent().User.Value;

            // Check if writing to the file is explicitly denied for this user or a group the user is in.
            if (rules.OfType<FileSystemAccessRule>().Any(r => (groups.Contains(r.IdentityReference) || r.IdentityReference.Value == sidCurrentUser) && r.AccessControlType == AccessControlType.Deny && (r.FileSystemRights & FileSystemRights.WriteData) == FileSystemRights.WriteData))
                return false;

            // Check if writing is allowed
            return rules.OfType<FileSystemAccessRule>().Any(r => (groups.Contains(r.IdentityReference) || r.IdentityReference.Value == sidCurrentUser) && r.AccessControlType == AccessControlType.Allow && (r.FileSystemRights & FileSystemRights.WriteData) == FileSystemRights.WriteData);
        }

        internal static string GetEngineFile()
        {
            return Path.Combine(GetGamePath(), "BioGame", "CookedPC", "Engine.u");
        }

        internal static void Backup2daFiles()
        {
            List<string> files = new List<string>();
            files.Add(Path.Combine("BioGame", "CookedPC", "Maps", "EntryMenu.SFM"));
            files.Add(Path.Combine("BioGame", "CookedPC", "Engine.u"));

            string gamepath = GetGamePath();
            string backuppath = Path.Combine(GetAppDataFolder(), "2dabackup");

            foreach (string file in files)
            {
                var sourcepath = Path.Combine(gamepath, file);
                var destpath = Path.Combine(backuppath, file);

                Directory.CreateDirectory(Directory.GetParent(destpath).FullName);
                File.Copy(sourcepath, destpath, true);
            }
            File.Create(Path.Combine(GetAppDataFolder(), "BACKED_UP"));
        }

        internal static string GetEntryMenuFile()
        {
            return Path.Combine(GetGamePath(), "BioGame", "CookedPC", "Maps", "EntryMenu.SFM");
        }

        public static bool IsDirectoryWritable2(string dirPath)
        {
            try
            {
                using (FileStream fs = File.Create(
                    Path.Combine(
                        dirPath,
                        Path.GetRandomFileName()
                    ),
                    1,
                    FileOptions.DeleteOnClose)
                )
                { }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// Gets the MEM game path. If the MEM game path is not set, the one from the registry is used.
        /// </summary>
        /// <param name="gameID"></param>
        /// <returns></returns>
        public static String GetGamePath()
        {
            bool UseMEMIni = false; //this can be turned on later if I want, but for now I only want legit.
            Utilities.WriteDebugLog("Looking up game path for Mass Effect.");
            //Read config file.
            string path = null;
            string mempath = null;
            string inipath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "MassEffectModder");
            inipath = Path.Combine(inipath, "MassEffectModder.ini");
            WriteDebugLog("MEM ini path " + inipath);

            if (File.Exists(inipath) && UseMEMIni)
            {
                WriteDebugLog("ini exists - loading mem ini");

                IniFile configIni = new IniFile(inipath);
                string key = "ME1";
                path = configIni.Read(key, "GameDataPath");
                if (path != null && path != "")
                {
                    path = path.TrimEnd(Path.DirectorySeparatorChar);
                    mempath = path;
                    WriteDebugLog("gamepath from mem ini: " + mempath);

                    string GameEXEPath = "";
                    GameEXEPath = Path.Combine(path, @"Binaries\MassEffect.exe");


                    if (!File.Exists(GameEXEPath))
                    {
                        WriteDebugLog("mem path has missing exe, not using mem path: " + GameEXEPath);
                        path = null; //mem path is not valid. might still be able to return later.
                    }
                    else
                    {
                        WriteDebugLog("Using mem path: " + GameEXEPath);
                        return path;
                    }
                }
                else
                {
                    WriteDebugLog("mem ini does not have path for this game.");
                }
            }

            //does not exist in ini (or ini does not exist).
            string softwareKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
            string key64 = @"Wow6432Node\";
            string gameKey = @"BioWare\Mass Effect";
            string entry = "Path";

            path = (string)Registry.GetValue(softwareKey + gameKey, entry, null);
            if (path == null)
            {
                path = (string)Registry.GetValue(softwareKey + key64 + gameKey, entry, null);
            }
            if (path != null)
            {
                WriteDebugLog("Found game path via registry: " + path);
                path = path.TrimEnd(Path.DirectorySeparatorChar);

                string GameEXEPath = Path.Combine(path, @"Binaries\MassEffect.exe");
                WriteDebugLog("GetGamePath Registry EXE Check Path: " + GameEXEPath);

                if (File.Exists(GameEXEPath))
                {
                    WriteDebugLog("EXE file exists - returning this path: " + GameEXEPath);
                    return path; //we have path now
                }
            }
            else
            {
                WriteDebugLog("Could not find game via registry.");
            }
            if (mempath != null && UseMEMIni)
            {
                WriteDebugLog("mem path not null and we allow MEM ini. Returning " + mempath);
                return mempath;
            }
            WriteDebugLog("No path found. Returning null");
            return null;
        }

        private static void WriteDebugLog(string v)
        {
            if (MainWindow.DEBUG_LOGGING)
            {
                Log.Debug(v);
            }
        }

        public static string GetGameEXEPath()
        {
            string path = GetGamePath();
            if (path == null) { return null; }
            WriteDebugLog("GetEXE ME1 Path: " + Path.Combine(path, @"Binaries\MassEffect.exe"));
            return Path.Combine(path, @"Binaries\MassEffect.exe");
        }

        public static bool IsDirectoryEmpty(string path)
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }

        internal static void WriteRegistryKey(RegistryKey subkey, string subpath, string value, string data)
        {
            int i = 0;
            string[] subkeys = subpath.Split('\\');
            while (i < subkeys.Length)
            {
                subkey = subkey.CreateSubKey(subkeys[i]);
                i++;
            }
            subkey.SetValue(value, data);
        }

        internal static void WriteRegistryKey(RegistryKey subkey, string subpath, string value, bool data)
        {
            WriteRegistryKey(subkey, subpath, value, data ? 1 : 0);
        }

        internal static void WriteRegistryKey(RegistryKey subkey, string subpath, string value, int data)
        {
            int i = 0;
            string[] subkeys = subpath.Split('\\');
            while (i < subkeys.Length)
            {
                subkey = subkey.CreateSubKey(subkeys[i]);
                i++;
            }
            subkey.SetValue(value, data);
        }

        public static string GetRegistrySettingString(string name)
        {
            string softwareKey = @"HKEY_CURRENT_USER\" + App.REGISTRY_KEY;
            return (string)Registry.GetValue(softwareKey, name, null);
        }

        public static string GetRegistrySettingString(string key, string name)
        {
            return (string)Registry.GetValue(key, name, null);
        }

        public static bool? GetRegistrySettingBool(string name)
        {
            string softwareKey = @"HKEY_CURRENT_USER\" + App.REGISTRY_KEY;

            int? value = (int?)Registry.GetValue(softwareKey, name, null);
            if (value != null)
            {
                return value > 0;
            }
            return null;
        }

        public static string GetGameBackupPath(int game)
        {
            string entry = "";
            string path = null;
            switch (game)
            {
                case 1:
                    entry = "ME1VanillaBackupLocation";
                    path = Utilities.GetRegistrySettingString(entry);
                    break;
                case 2:
                    entry = "ME2VanillaBackupLocation";
                    path = Utilities.GetRegistrySettingString(entry);
                    break;
                case 3:
                    //Check for backup via registry - Use Mod Manager's game backup key to find backup.
                    string softwareKey = @"HKEY_CURRENT_USER\SOFTWARE\Mass Effect 3 Mod Manager";
                    entry = "VanillaCopyLocation";
                    path = Utilities.GetRegistrySettingString(softwareKey, entry);
                    break;
                default:
                    return null;
            }
            if (path == null || !Directory.Exists(path))
            {
                return null;
            }
            if (!Directory.Exists(path + @"\BIOGame") || !Directory.Exists(path + @"\Binaries"))
            {
                return null;
            }
            return path;
        }

        // Pinvoke for API function
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
        out ulong lpFreeBytesAvailable,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes);

        public static bool DriveFreeBytes(string folderName, out ulong freespace)
        {
            freespace = 0;
            if (string.IsNullOrEmpty(folderName))
            {
                throw new ArgumentNullException("folderName");
            }

            if (!folderName.EndsWith("\\"))
            {
                folderName += '\\';
            }

            ulong free = 0, dummy1 = 0, dummy2 = 0;

            if (GetDiskFreeSpaceEx(folderName, out free, out dummy1, out dummy2))
            {
                freespace = free;
                return true;
            }
            else
            {
                return false;
            }
        }

        public static string GetRelativePath(string filespec, string folder)
        {
            Uri pathUri = new Uri(filespec);
            // Folders must end in a slash
            if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                folder += Path.DirectorySeparatorChar;
            }
            Uri folderUri = new Uri(folder);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }

        public static bool GetME1LAAEnabled()
        {
            string exePath = Utilities.GetGameEXEPath();
            if (File.Exists(exePath))
            {
                using (FileStream fs = new FileStream(exePath, FileMode.Open, FileAccess.Read))
                {
                    fs.JumpTo(0x3C); // jump to offset of COFF header
                    uint offset = fs.ReadUInt32() + 4; // skip PE signature too
                    fs.JumpTo(offset + 0x12); // jump to flags entry
                    ushort flag = fs.ReadUInt16(); // read flags
                    return (flag & 0x20) == 0x20; // check for LAA flag
                }
            }
            return false;
        }

        public static bool DeleteFilesAndFoldersRecursively(string target_dir)
        {
            bool result = true;
            foreach (string file in Directory.GetFiles(target_dir))
            {
                File.SetAttributes(file, FileAttributes.Normal); //remove read only
                try
                {
                    //Debug.WriteLine("Deleting file: " + file);
                    File.Delete(file);
                }
                catch (Exception e)
                {
                    Log.Error("Unable to delete file: " + file + ". It may be open still: " + e.Message);
                    return false;
                }
            }

            foreach (string subDir in Directory.GetDirectories(target_dir))
            {
                result &= DeleteFilesAndFoldersRecursively(subDir);
            }

            Thread.Sleep(4); // This makes the difference between whether it works or not. Sleep(0) is not enough.
            try
            {
                //Debug.WriteLine("Deleting directory: " + target_dir);

                Directory.Delete(target_dir);
            }
            catch (Exception e)
            {
                Log.Error("Unable to delete directory: " + target_dir + ". It may be open still. " + e.Message);
                return false;
            }
            return result;
        }

        public static string CalculateMD5(string filename)
        {
            try
            {
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(filename))
                    {
                        var hash = md5.ComputeHash(stream);
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
            }
            catch (IOException e)
            {
                Log.Error("I/O ERROR CALCULATING CHECKSUM OF FILE: " + filename);
                Log.Error("This is a critical error - this system may have hardware issues.");
                Log.Error(FlattenException(e));
                return "";
            }
        }

        internal static string GetALOTMarkerFilePath(int gameID)
        {
            string gamePath = GetGamePath();
            if (gamePath != null)
            {
                gamePath += @"\BioGame\CookedPC\testVolumeLight_VFX.upk";
                return gamePath;
            }
            return null;
        }

        public static string FlattenException(Exception exception)
        {
            var stringBuilder = new StringBuilder();

            while (exception != null)
            {
                stringBuilder.AppendLine(exception.Message);
                stringBuilder.AppendLine(exception.StackTrace);

                exception = exception.InnerException;
            }

            return stringBuilder.ToString();
        }

        public static ALOTVersionInfo GetInstalledALOTInfo(int gameID)
        {
            string gamePath = Utilities.GetALOTMarkerFilePath(gameID);
            if (gamePath != null && File.Exists(gamePath))
            {
                try
                {
                    using (FileStream fs = new FileStream(gamePath, System.IO.FileMode.Open, FileAccess.Read))
                    {
                        fs.SeekEnd();
                        long endPos = fs.Position;
                        fs.Position = endPos - 4;
                        uint memi = fs.ReadUInt32();

                        if (memi == MEMI_TAG)
                        {
                            //ALOT has been installed
                            fs.Position = endPos - 8;
                            int installerVersionUsed = fs.ReadInt32();
                            int perGameFinal4Bytes = -20;
                            switch (gameID)
                            {
                                case 1:
                                    perGameFinal4Bytes = 0;
                                    break;
                                case 2:
                                    perGameFinal4Bytes = 4352;
                                    break;
                                case 3:
                                    perGameFinal4Bytes = 16777472;
                                    break;
                            }

                            if (installerVersionUsed >= 10 && installerVersionUsed != perGameFinal4Bytes) //default bytes before 178 MEMI Format
                            {
                                fs.Position = endPos - 12;
                                short ALOTVER = fs.ReadInt16();
                                byte ALOTUPDATEVER = (byte)fs.ReadByte();
                                byte ALOTHOTFIXVER = (byte)fs.ReadByte();

                                //unused for now
                                fs.Position = endPos - 16;
                                int MEUITMVER = fs.ReadInt32();

                                return new ALOTVersionInfo(ALOTVER, ALOTUPDATEVER, ALOTHOTFIXVER, MEUITMVER);
                            }
                            else
                            {
                                return new ALOTVersionInfo(0, 0, 0, 0); //MEMI tag but no info we know of
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error("Error reading marker file for Mass Effect " + gameID + ". ALOT Info will be returned as null (nothing installed). " + e.Message);
                    return null;
                }
            }
            return null;
        }

        public static int runProcess(string exe, string args, bool standAlone = false)
        {
            Log.Information("Running process: " + exe + " " + args);
            using (Process p = new Process())
            {
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.FileName = exe;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.Arguments = args;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;


                StringBuilder output = new StringBuilder();
                StringBuilder error = new StringBuilder();

                using (AutoResetEvent outputWaitHandle = new AutoResetEvent(false))
                using (AutoResetEvent errorWaitHandle = new AutoResetEvent(false))
                {
                    p.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data == null)
                        {
                            outputWaitHandle.Set();
                        }
                        else
                        {
                            output.AppendLine(e.Data);
                        }
                    };
                    p.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data == null)
                        {
                            errorWaitHandle.Set();
                        }
                        else
                        {
                            error.AppendLine(e.Data);
                        }
                    };

                    p.Start();
                    if (!standAlone)
                    {
                        int timeout = 600000;
                        p.BeginOutputReadLine();
                        p.BeginErrorReadLine();

                        if (p.WaitForExit(timeout) &&
                            outputWaitHandle.WaitOne(timeout) &&
                            errorWaitHandle.WaitOne(timeout))
                        {
                            // Process completed. Check process.ExitCode here.
                            Log.Information("Process standard output of " + exe + " " + args + ":");
                            if (output.ToString().Length > 0)
                            {
                                Log.Information("Standard:\n" + output.ToString());
                            }
                            if (error.ToString().Length > 0)
                            {
                                Log.Error("Error output:\n" + error.ToString());
                            }
                            return p.ExitCode;
                        }
                        else
                        {
                            // Timed out.
                            Log.Error("Process timed out: " + exe + " " + args);
                            return -1;
                        }
                    }
                    else
                    {
                        return 0; //standalone
                    }
                }
            }
        }

        public static int runProcessAsAdmin(string exe, string args, bool standAlone = false)
        {
            Log.Information("Running process as admin: " + exe + " " + args);
            using (Process p = new Process())
            {
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.FileName = exe;
                p.StartInfo.UseShellExecute = true;
                p.StartInfo.Arguments = args;
                p.StartInfo.Verb = "runas";
                try
                {
                    p.Start();
                    if (!standAlone)
                    {
                        p.WaitForExit(60000);
                        try
                        {
                            return p.ExitCode;
                        }
                        catch (Exception e)
                        {
                            Log.Error("Error getting return code from admin process. It may have timed out.\n" + FlattenException(e));
                            return -1;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
                catch (System.ComponentModel.Win32Exception e)
                {
                    Log.Error("Error running elevated process: " + e.Message);
                    return WIN32_EXCEPTION_ELEVATED_CODE;
                }
            }
        }

        private static void SetAttrSafe(XmlNode node, params XmlAttribute[] attrList)
        {
            foreach (var attr in attrList)
            {
                if (node.Attributes[attr.Name] != null)
                {
                    node.Attributes[attr.Name].Value = attr.Value;
                }
                else
                {
                    node.Attributes.Append(attr);
                }
            }
        }

        public static long GetInstalledRamAmount()
        {
            long memKb;
            GetPhysicallyInstalledSystemMemory(out memKb);
            if (memKb == 0L)
            {
                uint errorcode = GetLastError();
                string errorMessage = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                Log.Warning("Failed to get RAM amount. This may indicate a potential (or soon coming) hardware problem. The error message was: " + errorMessage);
            }
            return memKb;
        }

        public static bool isRunningOnAMD()
        {
            var processorIdentifier = System.Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
            return processorIdentifier != null && processorIdentifier.Contains("AuthenticAMD");
        }

        public static bool TestXMLIsValid(string inputXML)
        {
            try
            {
                XDocument.Parse(inputXML);
                return true;
            }
            catch (XmlException)
            {
                return false;
            }
        }

        public static string sha256(string randomString)
        {
            System.Security.Cryptography.SHA256Managed crypt = new System.Security.Cryptography.SHA256Managed();
            System.Text.StringBuilder hash = new System.Text.StringBuilder();
            byte[] crypto = crypt.ComputeHash(Encoding.UTF8.GetBytes(randomString), 0, Encoding.UTF8.GetByteCount(randomString));
            foreach (byte theByte in crypto)
            {
                hash.Append(theByte.ToString("x2"));
            }
            return hash.ToString();
        }

        public static bool OpenAndSelectFileInExplorer(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                return false;
            }
            //Clean up file path so it can be navigated OK
            filePath = System.IO.Path.GetFullPath(filePath);
            System.Diagnostics.Process.Start("explorer.exe", string.Format("/select,\"{0}\"", filePath));
            return true;

        }

        public static bool IsWindowOpen<T>(string name = "") where T : Window
        {
            return string.IsNullOrEmpty(name)
               ? Application.Current.Windows.OfType<T>().Any()
               : Application.Current.Windows.OfType<T>().Any(w => w.Name.Equals(name));
        }

        public static long DirSize(DirectoryInfo d)
        {
            long size = 0;
            // Add file sizes.
            FileInfo[] fis = d.GetFiles();
            foreach (FileInfo fi in fis)
            {
                size += fi.Length;
            }
            // Add subdirectory sizes.
            DirectoryInfo[] dis = d.GetDirectories();
            foreach (DirectoryInfo di in dis)
            {
                size += DirSize(di);
            }
            return size;
        }

        public static bool IsSubfolder(string parentPath, string childPath)
        {
            var parentUri = new Uri(parentPath);
            var childUri = new DirectoryInfo(childPath).Parent;
            while (childUri != null)
            {
                if (new Uri(childUri.FullName) == parentUri)
                {
                    return true;
                }
                childUri = childUri.Parent;
            }
            return false;
        }

        public static void GetAntivirusInfo()
        {
            ManagementObjectSearcher wmiData = new ManagementObjectSearcher(@"root\SecurityCenter2", "SELECT * FROM AntivirusProduct");
            ManagementObjectCollection data = wmiData.Get();

            foreach (ManagementObject virusChecker in data)
            {
                var virusCheckerName = virusChecker["displayName"];
                var productState = virusChecker["productState"];
                uint productVal = (uint)productState;
                var bytes = BitConverter.GetBytes(productVal);
                Log.Information("Antivirus info: " + virusCheckerName + " with state " + bytes[1].ToString("X2") + " " + bytes[2].ToString("X2") + " " + bytes[3].ToString("X2"));
            }
        }

        public static bool isAntivirusRunning()
        {
            return true;
        }


        public static bool isGameRunning(int gameID)
        {
            Process[] pname = Process.GetProcessesByName("MassEffect");
            return pname.Length > 0;
        }
    }
}