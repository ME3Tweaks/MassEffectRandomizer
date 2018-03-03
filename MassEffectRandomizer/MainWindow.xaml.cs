using MahApps.Metro.Controls;
using MassEffectRandomizer.Classes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        public MainWindow()
        {
            EmbeddedDllClass.ExtractEmbeddedDlls("lzo2.dll", Properties.Resources.lzo2);
            EmbeddedDllClass.ExtractEmbeddedDlls("lzo2.dll", Properties.Resources.lzo2helper);
            EmbeddedDllClass.LoadDll("lzo2.dll");
            EmbeddedDllClass.LoadDll("lzo2helper.dll");
            InitializeComponent();
        }

        private void RandomizeButton_Click(object sender, RoutedEventArgs e)
        {

            RandomizeGalaxyMap();
        }

        private void RandomizeGalaxyMap()
        {
            ME1Package engine = new ME1Package(@"X:\Mass Effect Games HDD\Mass Effect\BioGame\CookedPC\Engine.u");
            foreach(IExportEntry export in engine.Exports)
            {
                if (export.ObjectName == "GalaxyMap_Cluster")
                {
                    Bio2DA cluster2da = new Bio2DA(export);
                    cluster2da.Write2DAToExport();
                }
            }
        }
    }
}
