using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MassEffectRandomizer.Classes;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MassEffectRandomizer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public static bool DEBUG_LOGGING { get; internal set; }

        public MainWindow()
        {
            EmbeddedDllClass.ExtractEmbeddedDlls("lzo2.dll", Properties.Resources.lzo2);
            EmbeddedDllClass.ExtractEmbeddedDlls("lzo2.dll", Properties.Resources.lzo2helper);
            EmbeddedDllClass.LoadDll("lzo2.dll");
            EmbeddedDllClass.LoadDll("lzo2helper.dll");
            InitializeComponent();
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            TextBlock_AssemblyVersion.Text = "Version " + version;
        }

        public async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            string me1Path = Utilities.GetGamePath();

            //int installedGames = 5;
            bool me1Installed = (me1Path != null);

            if (!me1Installed)
            {
                Log.Error("Mass Effect couldn't be found. Application will now exit.");
                await this.ShowMessageAsync("Mass Effect is not installed", "Mass Effect couldn't be found on this system. Mass Effect Randomizer only works with legitimate, official copies of Mass Effect. If you need assistance, please come to the ME3Tweaks Discord for assistance.");
                Log.Error("Exiting due to no games installed");
                Environment.Exit(1);
            }
            Log.Information("Game is installed at " + me1Path);
        }

        private void RandomizeButton_Click(object sender, RoutedEventArgs e)
        {
            Button_Randomize.Visibility = Visibility.Collapsed;
            Textblock_CurrentTask.Visibility = Visibility.Visible;
            Progressbar_Bottom.Visibility = Visibility.Visible;
            Randomizer randomizer = new Randomizer(this);
            randomizer.randomize();
        }

        private void Image_ME3Tweaks_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("https://me3tweaks.com");
            }
            catch (Exception ex)
            {

            }
        }
    }
}
