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

namespace GalaxyMapSWFBuilder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public string SelectedPath { get; set; }
        public ICommand RunBuilderCommand { get; private set; }
        public ICommand LoadImagesCommand { get; private set; }
        public ObservableCollectionExtended<GMImage> images { get; } = new ObservableCollectionExtended<GMImage>();
        public string ImageGroup { get; private set; }

        public MainWindow()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
            if (Directory.Exists(@"X:\Google Drive\Mass Effect Modding\MER\GalaxyMapImages\processed"))
            {
                LoadImagesInternal(@"X:\Google Drive\Mass Effect Modding\MER\GalaxyMapImages\processed"); //debug on ryzen
            } else if (Directory.Exists(@"C:\Users\Mgamerz\Google Drive\Mass Effect Modding\MER\GalaxyMapImages\processed")){
                LoadImagesInternal(@"C:\Users\Mgamerz\Google Drive\Mass Effect Modding\MER\GalaxyMapImages\processed");
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
            //throw new NotImplementedException();
            for (int i = 0; i < images.Count; i++)
            {
                ImageList.SelectedIndex = i;
                ImageList.ScrollIntoView(ImageList.SelectedItem);
                //I am too lazy to background thread this.
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                    new Action(delegate { }));
                string jpex = @"C:\Program Files (x86)\FFDec\ffdec.bat";
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
    }
}
