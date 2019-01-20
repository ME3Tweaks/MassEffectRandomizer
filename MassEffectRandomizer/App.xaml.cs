﻿using CommandLine;
using Serilog;
using Serilog.Sinks.RollingFile.Extension;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MassEffectRandomizer
{


    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        internal const string REGISTRY_KEY = @"Software\MassEffectRandomizer";
        internal const string BACKUP_REGISTRY_KEY = @"Software\ALOTAddon"; //Shared. Do not change

        [STAThread]
        public static void Main()
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            string exePath = assembly.Location;
            string exeFolder = Directory.GetParent(exePath).ToString();

            string[] args = Environment.GetCommandLineArgs();
            Parsed<Options> parsedCommandLineArgs = null;
            string updateDestinationPath = null;
            if (args.Length > 1)
            {
                var result = Parser.Default.ParseArguments<Options>(args);
                if (result.GetType() == typeof(Parsed<Options>))
                {
                    //Parsing succeeded - have to do update check to keep logs in order...
                    parsedCommandLineArgs = (Parsed<Options>)result;
                    if (parsedCommandLineArgs.Value.UpdateDest != null)
                    {
                        if (Directory.Exists(parsedCommandLineArgs.Value.UpdateDest))
                        {
                            updateDestinationPath = parsedCommandLineArgs.Value.UpdateDest;
                        }
                        if (parsedCommandLineArgs.Value.BootingNewUpdate)
                        {
                            Thread.Sleep(1000); //Delay boot to ensure update executable finishes
                            try
                            {
                                string updateFile = Path.Combine(exeFolder, "MassEffectRandomizer-Update.exe");
                                if (File.Exists(updateFile))
                                {
                                    File.Delete(updateFile);
                                    Log.Information("Deleted staged update");
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Warning("Unable to delete staged update: " + e.ToString());
                            }
                        }
                    }
                }
            }


            var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            Log.Logger = new LoggerConfiguration()
      .WriteTo.SizeRollingFile(Path.Combine(appdata, "MassEffectRandomizer", "logs", "applog.txt"),
              retainedFileDurationLimit: TimeSpan.FromDays(7),
              fileSizeLimitBytes: 1024 * 1024 * 10) // 10MB
#if DEBUG
      .WriteTo.Debug()
#endif
      .CreateLogger();
            Log.Information("===========================================================================");
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            string version = fvi.FileVersion;
            Log.Information("Mass Effect Randomizer " + version);
            Log.Information("Application boot: " + DateTime.UtcNow.ToString());

            if (updateDestinationPath != null)
            {
                Log.Information(" >> In update mode. Update destination: " + updateDestinationPath);
                int i = 0;
                while (i < 8)
                {

                    i++;
                    try
                    {
                        Log.Information("Applying update");
                        File.Copy(assembly.Location, updateDestinationPath, true);
                        Log.Information("Update applied, restarting...");
                        break;
                    }
                    catch (Exception e)
                    {
                        Log.Error("Error applying update: " + e.Message);
                        if (i < 5)
                        {
                            Thread.Sleep(1000);
                            Log.Warning("Attempt #" + (i + 1));
                        }
                        else
                        {
                            Log.Fatal("Unable to apply update after 8 attempts. We are giving up.");
                            MessageBox.Show("Update was unable to apply. See the application log for more information. If this continues to happen please come to the ME3Tweaks discord, or download a new copy from GitHub.");
                            Environment.Exit(1);
                        }
                    }
                }
                Log.Information("Rebooting into normal mode to complete update");
                ProcessStartInfo psi = new ProcessStartInfo(updateDestinationPath + System.AppDomain.CurrentDomain.FriendlyName);
                psi.WorkingDirectory = updateDestinationPath;
                psi.Arguments = "--completing-update";
                Process.Start(psi);
                Environment.Exit(0);
                Current.Shutdown();
            }




            var application = new App();
            application.InitializeComponent();
            application.Run();
        }
    }


    class Options
    {
        [Option('u', "update-dest-path",
          HelpText = "Indicates where this booting instance of Mass Effect Randomizer should attempt to copy itself and reboot to")]
        public string UpdateDest { get; set; }

        [Option('c', "completing-update",
            HelpText = "Indicates that we are booting a new copy of Mass Effect Randomizer that has just been upgraded")]
        public bool BootingNewUpdate { get; set; }
    }
}

