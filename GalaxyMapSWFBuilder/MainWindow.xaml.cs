using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
using System.Windows.Threading;
using System.Xml.Linq;

namespace GalaxyMapSWFBuilder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public string SelectedPath { get; set; }
        public string ImageListText { get; set; }
        public ICommand RunBuilderCommand { get; private set; }
        public ICommand LoadImagesCommand { get; private set; }
        public ObservableCollectionExtended<GMImage> images { get; } = new ObservableCollectionExtended<GMImage>();
        public string ImageGroup { get; private set; }

        public List<RandomizedPlanetInfo> AllPlanetInfos;
        public MainWindow()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
            if (Directory.Exists(@"X:\Google Drive\Mass Effect Modding\MER\GalaxyMapImages\processed"))
            {
                LoadImagesInternal(@"X:\Google Drive\Mass Effect Modding\MER\GalaxyMapImages\processed"); //debug on ryzen
            }
            else if (Directory.Exists(@"C:\Users\Mgamerz\Google Drive\Mass Effect Modding\MER\GalaxyMapImages\processed"))
            {
                LoadImagesInternal(@"C:\Users\Mgamerz\Google Drive\Mass Effect Modding\MER\GalaxyMapImages\processed");
            }
            AllPlanetInfos = GetPlanetInfos();
            var emptyImageGroups = AllPlanetInfos.Where(x => x.ImageGroup == null).ToList();
            if (emptyImageGroups.Count > 0)
            {
                Debugger.Break();
            }
            if (AllPlanetInfos.Count > 0)
            {
                //Run check for PlanetInfos ImageGroup's vs count of Categories

                //Get available groups and build lists of images
                var availableimagegroups = images.Select(c => c.Category.ToLower()).Distinct().ToList();
                var imageGroupLists = new Dictionary<string, List<GMImage>>();
                foreach (var availablegroup in availableimagegroups)
                {
                    imageGroupLists[availablegroup] = images.Where(c => c.Category.ToLower() == availablegroup).ToList();
                }

                var needsMoreImageCounters = new Dictionary<string, int>();
                foreach (var planetInfo in AllPlanetInfos)
                {
                    bool noImage = false;
                    if (imageGroupLists.TryGetValue(planetInfo.ImageGroup.ToLower(), out List<GMImage> availableImages))
                    {
                        if (availableImages.Count > 0)
                        {
                            availableImages.RemoveAt(0); //Remove one from the group
                        }
                        else
                        {
                            noImage = true;
                        }
                    }
                    else
                    {
                        noImage = true;
                    }

                    if (noImage)
                    {
                        if (needsMoreImageCounters.TryGetValue(planetInfo.ImageGroup.ToLower(), out int counter))
                        {
                            needsMoreImageCounters[planetInfo.ImageGroup.ToLower()] = counter + 1;
                        }
                        else
                        {
                            needsMoreImageCounters[planetInfo.ImageGroup.ToLower()] = 1;
                        }
                    }
                }

                foreach (var counter in needsMoreImageCounters)
                {
                    Debug.WriteLine("Not enough images for group " + counter.Key + ": Needs " + counter.Value + " more images");
                }

                foreach (var group in imageGroupLists)
                {
                    if (group.Value.Count > 0)
                    {
                        Debug.WriteLine("Remaining images in group " + group.Key + ": " + group.Value.Count);
                    }
                    else if (needsMoreImageCounters.FirstOrDefault(x => x.Key == group.Key).Key == null)
                    {
                        Debug.WriteLine("Exact amount of images in group " + group.Key + ": " + group.Value.Count);
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void LoadCommands()
        {
            RunBuilderCommand = new GenericCommand(RunBuilder, CanRunBuilder);
            LoadImagesCommand = new GenericCommand(LoadImages, () => true);
        }

        private void LoadImages()
        {
            LoadImagesInternal();
        }

        private void LoadImagesInternal(string path = null)
        {
            if (path == null)
            {
                CommonOpenFileDialog m = new CommonOpenFileDialog();
                m.IsFolderPicker = true;
                m.EnsurePathExists = true;
                m.Title = "Select galaxy map images folder";
                if (m.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    path = m.FileName;
                }
                else
                {
                    return;
                }
            }

            var files = Directory.GetFiles(path, "*.jpg", SearchOption.AllDirectories);
            images.ReplaceAll(files.Select(x => new GMImage(x)));
        }

        private void RunBuilder()
        {
            string jpex = @"C:\Program Files (x86)\FFDec\ffdec.bat";
            for (int i = 0; i < images.Count; i++)
            {
                ImageList.SelectedIndex = i;
                ImageList.ScrollIntoView(ImageList.SelectedItem);
                //I am too lazy to background thread this.
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                    new Action(delegate { }));
                Process process = new Process();
                // Configure the process using the StartInfo properties.
                process.StartInfo.FileName = jpex;
                GMImage image = images[i];
                Directory.CreateDirectory(image.Category);
                process.StartInfo.Arguments = $"-replace singleimage.swf {image.Category}\\{Path.GetFileNameWithoutExtension(image.ShortName)}.swf 1 \"{image.FilepathToImage}\"";
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.Start();
                process.WaitForExit(); // Waits here for the process to exit.
            }
        }

        private bool CanRunBuilder() => images.Any();

        public class GMImage
        {

            public GMImage(string x)
            {
                this.FilepathToImage = x;
            }

            public string FilepathToImage { get; set; }
            public string ShortName => Path.GetFileName(FilepathToImage);
            public string Category => Directory.GetParent(FilepathToImage).Name;
        }

        private void ImageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                GMImage gm = (GMImage)e.AddedItems[0];
                ImageToDisplay.Source = new BitmapImage(new Uri(gm.FilepathToImage));
                ImageGroup = gm.Category;
            }
        }



        #region MER specific code
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

        private List<RandomizedPlanetInfo> GetPlanetInfos()
        {
            string filepath = @"C:\Users\mgame\source\repos\MassEffectRandomizer\MassEffectRandomizer\staticfiles\text\planetinfo.xml";
            if (!File.Exists(filepath))
            {
                filepath = @"C:\Users\Mgamerz\source\repos\MassEffectRandomizer\MassEffectRandomizer\staticfiles\text\planetinfo.xml";
            }
            if (File.Exists(filepath))
            {
                XElement rootElement = XElement.Load(filepath);
                return (from e in rootElement.Elements("RandomizedPlanetInfo")
                        select new RandomizedPlanetInfo
                        {
                            PlanetName = (string)e.Element("PlanetName"),
                            PlanetName2 = (string)e.Element("PlanetName2"), //Original name (plot planets only)
                            PlanetDescription = (string)e.Element("PlanetDescription"),
                            IsMSV = (bool)e.Element("IsMSV"),
                            IsAsteroidBelt = (bool)e.Element("IsAsteroidBelt"),
                            IsAsteroid = e.Element("IsAsteroid") != null && (bool)e.Element("IsAsteroid"),
                            PreventShuffle = (bool)e.Element("PreventShuffle"),
                            RowID = (int)e.Element("RowID"),
                            MapBaseNames = e.Elements("MapBaseNames")
                                .Select(r => r.Value).ToList(),
                            DLC = e.Element("DLC")?.Value,
                            ImageGroup = e.Element("ImageGroup")?.Value
                        }).Where(x=>!x.IsAsteroidBelt).ToList();
            }
            return new List<RandomizedPlanetInfo>();
        }
        #endregion
    }
}
