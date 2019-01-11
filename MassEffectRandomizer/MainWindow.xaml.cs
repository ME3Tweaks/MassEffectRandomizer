using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MassEffectRandomizer.Classes;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {
        public static bool DEBUG_LOGGING { get; internal set; }
        public enum RandomizationMode
        {
            ERandomizationMode_SelectAny = 0,
            ERandomizationMode_Common = 1,
            ERAndomizationMode_Screed = 2
        }

        private RandomizationMode _selectedRandomizationMode;
        public RandomizationMode SelectedRandomizeMode
        {
            get { return _selectedRandomizationMode; }
            set { SetProperty(ref _selectedRandomizationMode, value); UpdateCheckboxSettings(); }
        }

        private int _currentProgress;
        public int CurrentProgressValue
        {
            get { return _currentProgress; }
            set { SetProperty(ref _currentProgress, value); }
        }

        private string _currentOperationText;
        public string CurrentOperationText
        {
            get { return _currentOperationText; }
            set { SetProperty(ref _currentOperationText, value); }
        }

        private double _progressbar_bottom_min;
        public double ProgressBar_Bottom_Min
        {
            get { return _progressbar_bottom_min; }
            set { SetProperty(ref _progressbar_bottom_min, value); }
        }

        private double _progressbar_bottom_max;
        public double ProgressBar_Bottom_Max
        {
            get { return _progressbar_bottom_max; }
            set { SetProperty(ref _progressbar_bottom_max, value); }
        }

        private Visibility _progressbar_visible;
        public Visibility ProgressBarVisible
        {
            get { return _progressbar_visible; }
            set { SetProperty(ref _progressbar_visible, value); }
        }

        private bool _progressbar_indeterminate;
        public bool ProgressBarIndeterminate { get { return _progressbar_indeterminate; } set { SetProperty(ref _progressbar_indeterminate, value); } }


        private void UpdateCheckboxSettings()
        {
            //both as common requires clear
            if (SelectedRandomizeMode == RandomizationMode.ERandomizationMode_SelectAny || SelectedRandomizeMode == RandomizationMode.ERandomizationMode_Common)
            {
                foreach (CheckBox cb in FindVisualChildren<CheckBox>(randomizationOptionsPanel))
                {
                    // do something with cb here
                    cb.IsChecked = false;
                }
            }

            if (SelectedRandomizeMode == RandomizationMode.ERandomizationMode_Common)
            {
                RANDSETTING_GALAXYMAP_CLUSTERS = true;
                RANDSETTING_GALAXYMAP_SYSTEMS = true;
                RANDSETTING_GALAXYMAP_PLANETCOLOR = true;
                RANDSETTING_WEAPONS_STARTINGEQUIPMENT = true;
                RANDSETTING_CHARACTER_INVENTORY = true;

                //testing only
                RANDSETTING_CHARACTER_HENCH_ARCHETYPES = true;
                RANDSETTING_CHARACTER_CHARCREATOR = true;
            }
            else if (SelectedRandomizeMode == RandomizationMode.ERAndomizationMode_Screed)
            {
                foreach (CheckBox cb in FindVisualChildren<CheckBox>(randomizationOptionsPanel))
                {
                    // do something with cb here
                    cb.IsChecked = true;
                }
            }
        }

        //RANDOMIZATION OPTION BINDINGS
        //Galaxy Map
        private bool _randsetting_galaxymap_planetcolor;
        public bool RANDSETTING_GALAXYMAP_PLANETCOLOR { get { return _randsetting_galaxymap_planetcolor; } set { SetProperty(ref _randsetting_galaxymap_planetcolor, value); } }

        private bool _randsetting_galaxymap_systems;
        public bool RANDSETTING_GALAXYMAP_SYSTEMS { get { return _randsetting_galaxymap_systems; } set { SetProperty(ref _randsetting_galaxymap_systems, value); } }

        private bool _randsetting_galaxymap_clusters;
        public bool RANDSETTING_GALAXYMAP_CLUSTERS { get { return _randsetting_galaxymap_clusters; } set { SetProperty(ref _randsetting_galaxymap_clusters, value); } }

        //Weapons
        private bool _randsetting_weapons_startingequipment;
        public bool RANDSETTING_WEAPONS_STARTINGEQUIPMENT { get { return _randsetting_weapons_startingequipment; } set { SetProperty(ref _randsetting_weapons_startingequipment, value); } }

        private bool _randsetting_weapons_effectlevels;
        public bool RANDSETTING_WEAPONS_EFFECTLEVELS { get { return _randsetting_weapons_effectlevels; } set { SetProperty(ref _randsetting_weapons_effectlevels, value); } }


        //Character
        private bool _randsetting_character_hench_archetypes;
        public bool RANDSETTING_CHARACTER_HENCH_ARCHETYPES { get { return _randsetting_character_hench_archetypes; } set { SetProperty(ref _randsetting_character_hench_archetypes, value); } }

        private bool _randsetting_character_inventory;
        public bool RANDSETTING_CHARACTER_INVENTORY { get { return _randsetting_character_inventory; } set { SetProperty(ref _randsetting_character_inventory, value); } }

        private bool _randsetting_character_charactercreator;
        public bool RANDSETTING_CHARACTER_CHARCREATOR { get { return _randsetting_character_charactercreator; } set { SetProperty(ref _randsetting_character_charactercreator, value); } }

        private bool _randsetting_character_charactercreator_skintone;
        public bool RANDSETTING_CHARACTER_CHARCREATOR_SKINTONE { get { return _randsetting_character_charactercreator_skintone; } set { SetProperty(ref _randsetting_character_charactercreator_skintone, value); } }

        private bool _randsetting_character_henchface;
        public bool RANDSETTING_CHARACTER_HENCHFACE { get { return _randsetting_character_henchface; } set { SetProperty(ref _randsetting_character_henchface, value); } }

        private bool _randsetting_character_iconicface;
        public bool RANDSETTING_CHARACTER_ICONICFACE { get { return _randsetting_character_iconicface; } set { SetProperty(ref _randsetting_character_iconicface, value); } }


        //Talents
        private bool _randsetting_talents_classtalents;
        public bool RANDSETTING_TALENTS_SHUFFLECLASSTALENTS { get { return _randsetting_talents_classtalents; } set { SetProperty(ref _randsetting_talents_classtalents, value); } }

        private bool _randsetting_talents_stats;
        public bool RANDSETTING_TALENTS_STATS { get { return _randsetting_talents_stats; } set { SetProperty(ref _randsetting_talents_stats, value); } }

        
        //MOVEMENT
        private bool _randsetting_movement_creaturespeed;
        public bool RANDSETTING_MOVEMENT_CREATURESPEED { get { return _randsetting_movement_creaturespeed; } set { SetProperty(ref _randsetting_movement_creaturespeed, value); } }

        //Misc
        private bool _randsetting_misc_music;
        public bool RANDSETTING_MISC_MUSIC { get { return _randsetting_misc_music; } set { SetProperty(ref _randsetting_misc_music, value); } }

        private bool _randsetting_misc_guimusic;
        public bool RANDSETTING_MISC_GUIMUSIC { get { return _randsetting_misc_guimusic; } set { SetProperty(ref _randsetting_misc_guimusic, value); } }

        private bool _randsetting_misc_guisfx;
        public bool RANDSETTING_MISC_GUISFX { get { return _randsetting_misc_guisfx; } set { SetProperty(ref _randsetting_misc_guisfx, value); } }

        private bool _randsetting_misc_mapfaces;
        public bool RANDSETTING_MISC_MAPFACES { get { return _randsetting_misc_mapfaces; } set { SetProperty(ref _randsetting_misc_mapfaces, value); } }

        private double _randsetting_misc_mapfaces_amount;
        public double RANDSETTING_MISC_MAPFACES_AMOUNT { get { return _randsetting_misc_mapfaces_amount; } set { SetProperty(ref _randsetting_misc_mapfaces_amount, value); } }

        private bool _randsetting_misc_mappawnsizes;
        public bool RANDSETTING_MISC_MAPPAWNSIZES { get { return _randsetting_misc_mappawnsizes; } set { SetProperty(ref _randsetting_misc_mappawnsizes, value); } }





        //MAKO 
        //        BIOC_Base.u -> 4940 Default__BioAttributesPawnVehicle m_initialThrusterAmountMax
        //END RANDOMIZE OPTION BINDINGS

        public MainWindow()
        {
            EmbeddedDllClass.ExtractEmbeddedDlls("lzo2wrapper.dll", Properties.Resources.lzo2wrapper);
            EmbeddedDllClass.LoadDll("lzo2wrapper.dll");
            Random random = new Random();
            var preseed = random.Next();
            RANDSETTING_MISC_MAPFACES_AMOUNT = .3;
            ProgressBar_Bottom_Max = 100;
            ProgressBar_Bottom_Min = 0;
            ProgressBarVisible = Visibility.Collapsed;
            InitializeComponent();
            SeedTextBox.Text = preseed.ToString();
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            TextBlock_AssemblyVersion.Text = "Version " + version;
            DataContext = this;
            SelectedRandomizeMode = RandomizationMode.ERandomizationMode_SelectAny;
        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        #region Property Changed Notification
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Notifies listeners when given property is updated.
        /// </summary>
        /// <param name="propertyname">Name of property to give notification for. If called in property, argument can be ignored as it will be default.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion

        public async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateBackupButtonStatus();
            string me1Path = Utilities.GetGamePath();

            //int installedGames = 5;
            bool me1Installed = (me1Path != null);

            if (!me1Installed)
            {
                Log.Error("Mass Effect couldn't be found. Application will now exit.");
                await this.ShowMessageAsync("Mass Effect is not installed", "Mass Effect couldn't be found on this system. Mass Effect Randomizer only works with legitimate, official copies of Mass Effect. Ensure you have run the game at least once. If you need assistance, please come to the ME3Tweaks Discord.");
                Log.Error("Exiting due to game not being found");
                Environment.Exit(1);
            }
            GameLocationTextbox.Text = "Game Path: " + me1Path;
            Log.Information("Game is installed at " + me1Path);

        }

        private void RandomizeButton_Click(object sender, RoutedEventArgs e)
        {
            Button_Randomize.Visibility = Visibility.Collapsed;
            Textblock_CurrentTask.Visibility = Visibility.Visible;
            Progressbar_Bottom_Wrapper.Visibility = Visibility.Visible;
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

        private void Button_BackupRestore_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(System.IO.Path.Combine(Utilities.GetAppDataFolder(), "BACKED_UP")))
            {
                Utilities.Restore2DAFiles();
            }
            else
            {
                Utilities.Backup2daFiles();
            }
            UpdateBackupButtonStatus();
        }

        private void UpdateBackupButtonStatus()
        {
            if (File.Exists(System.IO.Path.Combine(Utilities.GetAppDataFolder(), "BACKED_UP")))
            {
                Button_BackupRestore.Content = "Restore 2DA files";
            }
            else
            {
                Button_BackupRestore.Content = "Backup 2DA files";
            }
        }
    }
}
