using Serilog;
using Serilog.Sinks.RollingFile.Extension;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
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
        internal const string BACKUP_REGISTRY_KEY = @"Software\ALOTAddon"; //Shared

        [STAThread]
        public static void Main()
        {
            var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            Log.Logger = new LoggerConfiguration()
      .WriteTo.SizeRollingFile(Path.Combine(appdata, "MassEffectRandomizer", "logs", "applog.txt"),
              retainedFileDurationLimit: TimeSpan.FromDays(7),
              fileSizeLimitBytes: 1024 * 1024 * 10) // 10MB
      .CreateLogger();


            var application = new App();
            application.InitializeComponent();
            application.Run();
        }
    }
}

