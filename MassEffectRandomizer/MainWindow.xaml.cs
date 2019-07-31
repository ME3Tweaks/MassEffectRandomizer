using ByteSizeLib;
using MahApps.Metro;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MassEffectRandomizer.Classes;
using MassEffectRandomizer.Classes.Updater;
using Octokit;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;

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

        private RandomizationMode _randomizationMode;

        public RandomizationMode SelectedRandomizeMode
        {
            get => _randomizationMode;
            set
            {
                SetProperty(ref _randomizationMode, value);
                UpdateCheckboxSettings();
            }
        }

        public int CurrentProgressValue { get; set; }
        public string CurrentOperationText { get; set; }
        public double ProgressBar_Bottom_Min { get; set; }
        public double ProgressBar_Bottom_Max { get; set; }
        public bool ProgressBarIndeterminate { get; set; }
        public Visibility ProgressPanelVisible { get; set; }
        public Visibility ButtonPanelVisible { get; set; }
        ProgressDialogController updateprogresscontroller;

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
                    if (cb.IsEnabled)
                    {
                        // do something with cb here
                        cb.IsChecked = true;
                    }
                }

            }
        }

        [DebuggerDisplay("RandomPlanetInfo ({PlanetName})")]
        public class RandomizedPlanetInfo
        {
            /// <summary>
            /// What 0-based row this planet information is for in the Bio2DA
            /// </summary>
            public int RowID;

            /// <summary>
            /// Prevents shuffling this item outside of it's row ID
            /// </summary>
            public bool PreventShuffle;

            /// <summary>
            /// Indicator that this is an MSV planet
            /// </summary>
            public bool IsMSV;

            /// <summary>
            /// Indicator that this is an Asteroid Belt
            /// </summary>
            public bool IsAsteroidBelt;

            /// <summary>
            /// Indicator that this is an Asteroid
            /// </summary>
            public bool IsAsteroid;

            /// <summary>
            /// Name to assign for randomization. If this is a plot planet, this value is the original planet name
            /// </summary>
            public string PlanetName;

            /// <summary>
            /// Name used for randomizing if it is a plot planet
            /// </summary>
            public string PlanetName2;

            public string PlanetDescription;

            /// <summary>
            /// WHen updating 2DA_AreaMap, labels that begin with these prefixes will be analyzed and updated accordingly by full (if no :) or anything before :.
            /// </summary>
            public List<string> MapBaseNames { get; internal set; }
            public string ImageGroup { get; internal set; }
            public string DLC { get; internal set; }
        }

        public class OpeningCrawl
        {
            public bool RequiresFaceRandomizer;
            public string CrawlText;
        }

        private void LoadPlanetsText()
        {
            string path = @"C:\users\mgame\desktop\planet_test.txt";
            var lines = File.ReadAllLines(path);


        }

        public ObservableCollectionExtended<ImageCredit> ImageCredits { get; } = new ObservableCollectionExtended<ImageCredit>();
        private void LoadImageCredits()
        {
            var textFile = Utilities.GetEmbeddedStaticFilesTextFile("imagecredits.txt").Split('\n');
            List<ImageCredit> credits = new List<ImageCredit>(330);

            ImageCredit currentCredit = null;
            for (int i = 0; i < textFile.Length; i++)
            {
                var trimmedline = textFile[i].Trim();
                if (trimmedline.StartsWith("#"))
                {
                    continue;
                }

                if (trimmedline == "" && currentCredit == null)
                {
                    currentCredit = new ImageCredit();
                    continue;
                }

                if (trimmedline == "")
                {
                    credits.Add(currentCredit);
                    currentCredit = new ImageCredit();
                    continue;
                }

                currentCredit.Title = textFile[i].Trim();
                currentCredit.Author = textFile[i + 1].Trim();
                currentCredit.InternalName = textFile[i + 2].Trim();
                currentCredit.Link = textFile[i + 3].Trim();
                currentCredit.License = textFile[i + 4].Trim();
                i += 4;
            }

            if (currentCredit != null)
            {
                credits.Add(currentCredit);
            }
            ImageCredits.ReplaceAll(credits);
        }

        //RANDOMIZATION OPTION BINDINGS
        //Galaxy Map
        public bool RANDSETTING_GALAXYMAP_PLANETCOLOR { get; set; }
        public bool RANDSETTING_GALAXYMAP_SYSTEMS { get; set; }
        public bool RANDSETTING_GALAXYMAP_CLUSTERS { get; set; }
        public bool RANDSETTING_GALAXYMAP_PLANETNAMEDESCRIPTION { get; set; }
        public bool RANDSETTING_GALAXYMAP_PLANETNAMEDESCRIPTION_PLOTPLANET { get; set; }


        //Weapons
        public bool RANDSETTING_WEAPONS_STARTINGEQUIPMENT { get; set; }
        public bool RANDSETTING_WEAPONS_EFFECTLEVELS { get; set; }


        //Character
        public bool RANDSETTING_CHARACTER_HENCH_ARCHETYPES { get; set; }
        public bool RANDSETTING_CHARACTER_INVENTORY { get; set; }
        public bool RANDSETTING_CHARACTER_CHARCREATOR { get; set; }
        public bool RANDSETTING_CHARACTER_CHARCREATOR_SKINTONE { get; set; }
        public bool RANDSETTING_CHARACTER_HENCHFACE { get; set; }
        public bool RANDSETTING_CHARACTER_ICONICFACE { get; set; }


        //Talents
        public bool RANDSETTING_TALENTS_SHUFFLECLASSTALENTS { get; set; }
        public bool RANDSETTING_TALENTS_STATS { get; set; }

        //private bool _randsetting_talents_shuffle_allowsquadmateunity;
        //public bool RANDSETTING_TALENTS_SHUFFLE_ALLOWSQUADMATEUNITY { get { return _randsetting_talents_shuffle_allowsquadmateunity; } set { SetProperty(ref _randsetting_talents_shuffle_allowsquadmateunity, value); } }



        //MOVEMENT
        public bool RANDSETTING_MOVEMENT_CREATURESPEED { get; set; }

        public bool RANDSETTING_MOVEMENT_MAKO { get; set; }

        //Misc
        public bool RANDSETTING_MISC_MUSIC { get; set; }
        public bool RANDSETTING_MISC_GUIMUSIC { get; set; }
        public bool RANDSETTING_MISC_GUISFX { get; set; }
        public bool RANDSETTING_MISC_MAPFACES { get; set; }
        public double RANDSETTING_MISC_MAPFACES_AMOUNT { get; set; }
        public int RANDSETTING_WACK_FACEFX_AMOUNT { get; set; }
        public bool LogUploaderFlyoutOpen { get; set; }
        public bool DiagnosticsFlyoutOpen { get; set; }
        public bool RANDSETTING_MISC_GAMEOVERTEXT { get; set; }
        public bool RANDSETTING_MISC_SPLASH { get; set; }
        public bool RANDSETTING_PAWN_MATERIALCOLORS { get; set; }

        //Wackadoodle
        public bool RANDSETTING_MISC_MAPPAWNSIZES { get; set; }
        public bool RANDSETTING_MISC_HAZARDS { get; set; }
        public bool RANDSETTING_MISC_ENEMYAIDISTANCES { get; set; }
        public bool RANDSETTING_MISC_INTERPS { get; set; }
        public bool RANDSETTING_WACK_OPENINGCUTSCENE { get; set; }
        public bool RANDSETTING_WACK_FACEFX { get; set; }
        public bool RANDSETTING_WACK_SCOTTISH { get; set; }


        //MAKO 
        //        BIOC_Base.u -> 4940 Default__BioAttributesPawnVehicle m_initialThrusterAmountMax
        //END RANDOMIZE OPTION BINDINGS

        public MainWindow()
        {
            DataContext = this;
            EmbeddedDllClass.ExtractEmbeddedDlls("lzo2wrapper.dll", Properties.Resources.lzo2wrapper);
            EmbeddedDllClass.LoadDll("lzo2wrapper.dll");
            Random random = new Random();
            var preseed = random.Next();
            RANDSETTING_MISC_MAPFACES_AMOUNT = .3;
            RANDSETTING_WACK_FACEFX_AMOUNT = 2;
            ProgressBar_Bottom_Max = 100;
            ProgressBar_Bottom_Min = 0;
            ProgressPanelVisible = Visibility.Visible;
            ButtonPanelVisible = Visibility.Collapsed;
            LoadImageCredits();
            InitializeComponent();
            SeedTextBox.Text = 529572808.ToString(); //preseed.ToString(); //DEBUG!
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            TextBlock_AssemblyVersion.Text = "Version " + version;
            Title += " " + version;
            SelectedRandomizeMode = RandomizationMode.ERandomizationMode_SelectAny;
            PerformUpdateCheck();

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

            string path = Utilities.GetGameBackupPath();
            if (path != null && me1Path != null)
            {
                BackupRestoreText = "Restore";
                BackupRestore_Button.ToolTip = "Click to restore game from " + Environment.NewLine + path;
            }
            else
            {
                if (me1Installed)
                {
                    BackupRestoreText = "Backup";
                    BackupRestore_Button.ToolTip = "Click to backup game";
                }
            }
        }

        public string BackupRestoreText { get; set; }

        private void RandomizeButton_Click(object sender, RoutedEventArgs e)
        {
            ButtonPanelVisible = Visibility.Collapsed;
            ProgressPanelVisible = Visibility.Visible;
            Randomizer randomizer = new Randomizer(this);
            randomizer.randomize();
        }

        private void Image_ME3Tweaks_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("https://me3tweaks.com");
            }
            catch (Exception)
            {

            }
        }

        private async void PerformUpdateCheck()
        {
            Log.Information("Checking for application updates from gitub");
            ProgressBarIndeterminate = true;
            CurrentOperationText = "Checking for application updates";
            var versInfo = System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
            var client = new GitHubClient(new ProductHeaderValue("MassEffectRandomizer"));
            try
            {
                int myReleaseAge = 0;
                var releases = await client.Repository.Release.GetAll("ME3Tweaks", "MassEffectRandomizer");
                if (releases.Count > 0)
                {
                    Log.Information("Fetched application releases from github");

                    //The release we want to check is always the latest, so [0]
                    Release latest = null;
                    Version latestVer = new Version("0.0.0.0");
                    foreach (Release r in releases)
                    {
                        if (r.Assets.Count > 0)
                        {
                            Version releaseVersion = new Version(r.TagName);
                            if (versInfo < releaseVersion)
                            {
                                myReleaseAge++;
                            }

                            if (releaseVersion > latestVer)
                            {
                                latest = r;
                                latestVer = releaseVersion;
                            }
                        }
                    }

                    if (latest != null)
                    {
                        Log.Information("Latest available: " + latest.TagName);
                        Version releaseName = new Version(latest.TagName);
                        if (versInfo < releaseName && latest.Assets.Count > 0)
                        {
                            bool upgrade = false;
                            bool canCancel = true;
                            Log.Information("Latest release is applicable to us.");

                            string versionInfo = "";
                            int daysAgo = (DateTime.Now - latest.PublishedAt.Value).Days;
                            string ageStr = "";
                            if (daysAgo == 1)
                            {
                                ageStr = "1 day ago";
                            }
                            else if (daysAgo == 0)
                            {
                                ageStr = "today";
                            }
                            else
                            {
                                ageStr = daysAgo + " days ago";
                            }

                            versionInfo += "\nReleased " + ageStr;
                            MetroDialogSettings mds = new MetroDialogSettings();
                            mds.AffirmativeButtonText = "Update";
                            mds.NegativeButtonText = "Later";
                            mds.DefaultButtonFocus = MessageDialogResult.Affirmative;

                            //MessageDialogResult result = await this.ShowMessageAsync("Update Available", "ALOT Installer " + releaseName + " is available. You are currently using version " + versInfo.ToString() + ".\n========================\n" + versionInfo + "\n" + latest.Body + "\n========================\nInstall the update?", MessageDialogStyle.AffirmativeAndNegative, mds);
                            //upgrade = result == MessageDialogResult.Affirmative;
                            string message = "Mass Effect Randomizer " + releaseName + " is available. You are currently using version " + versInfo.ToString() + "." + versionInfo;
                            UpdateAvailableDialog uad = new UpdateAvailableDialog(message, latest.Body, this);
                            await this.ShowMetroDialogAsync(uad, mds);
                            await uad.WaitUntilUnloadedAsync();
                            upgrade = uad.wasUpdateAccepted();

                            if (upgrade)
                            {
                                Log.Information("Downloading update for application");

                                //there's an update
                                message = "Downloading update...";

                                updateprogresscontroller = await this.ShowProgressAsync("Downloading update", message, canCancel);
                                updateprogresscontroller.SetIndeterminate();
                                WebClient downloadClient = new WebClient();

                                downloadClient.Headers["Accept"] = "application/vnd.github.v3+json";
                                downloadClient.Headers["user-agent"] = "MassEffectRandomizer";
                                string temppath = System.IO.Path.GetTempPath();
                                int downloadProgress = 0;
                                downloadClient.DownloadProgressChanged += (s, e) =>
                                {
                                    if (downloadProgress != e.ProgressPercentage)
                                    {
                                        Log.Information("Program update download percent: " + e.ProgressPercentage);
                                    }

                                    string downloadedStr = ByteSize.FromBytes(e.BytesReceived).ToString() + " of " + ByteSize.FromBytes(e.TotalBytesToReceive).ToString();
                                    updateprogresscontroller.SetMessage(message + "\n\n" + downloadedStr);

                                    downloadProgress = e.ProgressPercentage;
                                    updateprogresscontroller.SetProgress((double)e.ProgressPercentage / 100);
                                };
                                updateprogresscontroller.Canceled += async (s, e) =>
                                {
                                    if (downloadClient != null)
                                    {
                                        Log.Information("Application update was in progress but was canceled.");
                                        downloadClient.CancelAsync();
                                        await updateprogresscontroller.CloseAsync();
                                    }
                                };
                                downloadClient.DownloadFileCompleted += UpdateDownloadCompleted;
                                string downloadPath = System.IO.Path.Combine(temppath, "MassEffectRandomizer-Update.exe");
                                //DEBUG ONLY
                                Uri downloadUri = new Uri(latest.Assets[0].BrowserDownloadUrl);
                                downloadClient.DownloadFileAsync(downloadUri, downloadPath, new KeyValuePair<ProgressDialogController, string>(updateprogresscontroller, downloadPath));
                            }
                            else
                            {
                                Log.Warning("Application update was declined");
                            }
                        }
                        else
                        {
                            //up to date
                            CurrentOperationText = "Application up to date";
                        }
                    }
                }
                else
                {
                    Log.Information("No releases found on Github");
                }
            }
            catch (Exception e)
            {
                Log.Error("Error checking for update: " + e);
            }

            ProgressPanelVisible = Visibility.Collapsed;
            ButtonPanelVisible = Visibility.Visible;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // close all active threads
            Environment.Exit(0);
        }

        private void UpdateDownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            Log.Information("Update downloaded - rebooting to new downloaded file, in update mode");
            string temppath = System.IO.Path.GetTempPath();
            string exe = System.IO.Path.Combine(temppath, "MassEffectRandomizer-Update.exe");

            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            string exePath = assembly.Location;

            string args = "--update-dest-path \"" + exePath + "\"";
            Utilities.runProcess(exe, args, true);
            while (true)
            {
                try
                {
                    Environment.Exit(0);
                }
                catch (TaskCanceledException)
                {
                    //something to do with how shutting down works.
                }
            }
        }

        private void Logs_Click(object sender, RoutedEventArgs e)
        {
            LogUploaderFlyoutOpen = true;
        }

        private void Diagnostics_Click(object sender, RoutedEventArgs e)
        {
            DiagnosticsFlyoutOpen = true;
        }

        private void InstallingOverlayFlyout_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private async void BackupRestore_Click(object sender, RoutedEventArgs e)
        {
            string path = Utilities.GetGameBackupPath();
            if (path != null)
            {
                MetroDialogSettings settings = new MetroDialogSettings();
                settings.NegativeButtonText = "Cancel";
                settings.AffirmativeButtonText = "Restore";
                MessageDialogResult result = await this.ShowMessageAsync("Restoring Mass Effect from backup", "Restoring Mass Effect will wipe out the current installation and put your game back to the state when you backed it up. Are you sure you want to do this?", MessageDialogStyle.AffirmativeAndNegative, settings);
                if (result == MessageDialogResult.Affirmative)
                {
                    RestoreGame();
                }
            }
            else
            {
                BackupGame();
            }
        }

        private void DebugCloseDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            DiagnosticsFlyoutOpen = false;
        }

        public class ImageCredit : NotifyPropertyChangedBase
        {
            public string Title { get; internal set; }
            public string Author { get; internal set; }
            public string InternalName { get; internal set; }
            public string Link { get; internal set; }
            public string License { get; internal set; }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                var hl = (Hyperlink) sender;
                System.Diagnostics.Process.Start((hl.DataContext as ImageCredit).Link);
            }
            catch (Exception)
            {

            }
        }
    }
}