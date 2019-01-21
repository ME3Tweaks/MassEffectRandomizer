using ByteSizeLib;
using Flurl.Http;
using MahApps.Metro;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MassEffectRandomizer.Classes;
using Serilog;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MassEffectRandomizer
{
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {
        public string WatermarkText { get; set; }

        public void InitLogUploaderUI()
        {
            Combobox_LogSelector.Items.Clear();
            var directory = new DirectoryInfo(App.LogDir);
            var logfiles = directory.GetFiles("applog*.txt").OrderByDescending(f => f.LastWriteTime).ToList();
            foreach (var file in logfiles)
            {
                Combobox_LogSelector.Items.Add(new LogItem(file.FullName));
            }
            if (Combobox_LogSelector.Items.Count > 0)
            {
                Combobox_LogSelector.SelectedIndex = 0;
            }
        }


        public string GetSelectedLogText()
        {
            //if (UserPressedUpload)
            //{
            string logpath = ((LogItem)Combobox_LogSelector.SelectedValue).filepath;
            string temppath = logpath + ".tmp";
            File.Copy(logpath, temppath);
            string log = File.ReadAllText(temppath);
            File.Delete(temppath);
            return log;
            //}
            //return null; //user clicked close X
        }

        private void Combobox_LogSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WatermarkText = Combobox_LogSelector.SelectedIndex == 0 ? "Latest log" : "Older log";
        }

        private void LogUploaderFlyout_IsOpenChanged(object sender, RoutedEventArgs e)
        {
            if (LogUploaderFlyout.IsOpen)
            {
                InitLogUploaderUI();
                ThemeManager.ChangeAppStyle(System.Windows.Application.Current,
                                                        ThemeManager.GetAccent("Cyan"),
                                                        ThemeManager.GetAppTheme("BaseDark")); // or appStyle.Item1
            }
            else
            {
                ThemeManager.ChangeAppStyle(System.Windows.Application.Current,
                                                        ThemeManager.GetAccent(App.MainThemeColor),
                                                        ThemeManager.GetAppTheme("BaseDark")); // or appStyle.Item1
            }
        }

        private async Task<string> UploadLog(bool isPreviousCrashLog, string log, bool openPageWhenFinished = true)
        {
            Log.Information("Preparing to upload randomizer log");
            string randomizerVer = System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString();
            var lzmaExtractedPath = Path.Combine(Path.GetTempPath(), "lzma.exe");

            //Extract LZMA so we can compress log for upload
            using (Stream stream = Utilities.GetResourceStream("MassEffectRandomizer.staticfiles.lzma.exe"))
            {
                using (var file = new FileStream(lzmaExtractedPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(file);
                }
            }


            if (log == null)
            {
                //latest
                var directory = new DirectoryInfo(App.LogDir);
                var logfiles = directory.GetFiles("applog*.txt").OrderByDescending(f => f.LastWriteTime).ToList();
                if (logfiles.Count() > 0)
                {
                    var currentTime = DateTime.Now;
                    log = "";
                    //if (currentTime.Date != bootTime.Date && logfiles.Count() > 1)
                    //{
                    //    //we need to do last 2 files
                    //    Log.Information("Log file has rolled over since app was booted - including previous days' log.");
                    //    File.Copy(logfiles.ElementAt(1).FullName, logfiles.ElementAt(1).FullName + ".tmp");
                    //    log = File.ReadAllText(logfiles.ElementAt(1).FullName + ".tmp");
                    //    File.Delete(logfiles.ElementAt(1).FullName + ".tmp");
                    //    log += "\n";
                    //}
                    Log.Information("Staging log file for upload. This is the final log item that should appear in an uploaded log.");
                    File.Copy(logfiles.ElementAt(0).FullName, logfiles.ElementAt(0).FullName + ".tmp");
                    log += File.ReadAllText(logfiles.ElementAt(0).FullName + ".tmp");
                    File.Delete(logfiles.ElementAt(0).FullName + ".tmp");
                }
                else
                {
                    Log.Information("No logs available, somehow. Canceling upload");
                    return null;
                }
            }

            string zipStaged = Path.Combine(Utilities.GetAppDataFolder(), "logfile_forUpload");
            File.WriteAllText(zipStaged, log);

            //Compress with LZMA for VPS Upload
            string outfile = "logfile_forUpload.lzma";
            string args = "e \"" + zipStaged + "\" \"" + outfile + "\" -mt2";
            Utilities.runProcess(lzmaExtractedPath, args);
            File.Delete(zipStaged);
            File.Delete(lzmaExtractedPath);
            var lzmalog = File.ReadAllBytes(outfile);
            File.Delete(outfile);

            ProgressDialogController progresscontroller = await this.ShowProgressAsync("Uploading log", "Log is currently uploading, please wait...", true);
            progresscontroller.SetIndeterminate();
            try
            {
                var responseString = await "https://vps.me3tweaks.com/masseffectrandomizer/logupload.php".PostUrlEncodedAsync(new { LogData = Convert.ToBase64String(lzmalog), MassEffectRandomizerVersion = randomizerVer, Type = "log", CrashLog = isPreviousCrashLog }).ReceiveString();
                Uri uriResult;
                bool result = Uri.TryCreate(responseString, UriKind.Absolute, out uriResult)
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                if (result)
                {
                    //should be valid URL.
                    //diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_Upload));
                    //e.Result = responseString;
                    await progresscontroller.CloseAsync();
                    Log.Information("Result from server for log upload: " + responseString);
                    if (openPageWhenFinished)
                    {
                        Utilities.OpenWebPage(responseString);
                    }
                    LogUploaderFlyoutOpen = false;
                    return responseString;
                }
                else
                {
                    File.Delete(outfile);

                    //diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAG_TEXT, "Error from oversized log uploader: " + responseString));
                    //diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_RED, Image_Upload));
                    await progresscontroller.CloseAsync();
                    Log.Error("Error uploading log. The server responded with: " + responseString);
                    //e.Result = "Diagnostic complete.";
                    await this.ShowMessageAsync("Log upload error", "The server rejected the upload. The response was: " + responseString);
                    //Utilities.OpenAndSelectFileInExplorer(diagfilename);
                }
            }
            catch (FlurlHttpTimeoutException)
            {
                // FlurlHttpTimeoutException derives from FlurlHttpException; catch here only
                // if you want to handle timeouts as a special case
                await progresscontroller.CloseAsync();
                Log.Error("Request timed out while uploading log.");
                await this.ShowMessageAsync("Log upload timed out", "The log took too long to upload. You will need to upload your log manually.");

            }
            catch (Exception ex)
            {
                // ex.Message contains rich details, inclulding the URL, verb, response status,
                // and request and response bodies (if available)
                await progresscontroller.CloseAsync();
                Log.Error("Handled error uploading log: " + Utilities.FlattenException(ex));
                string exmessage = ex.Message;
                var index = exmessage.IndexOf("Request body:");
                if (index > 0)
                {
                    exmessage = exmessage.Substring(0, index);
                }
                await this.ShowMessageAsync("Log upload failed", "The log was unable to upload. The error message is: " + exmessage + "You will need to upload your log manually.");
            }
            LogUploaderFlyoutOpen = false;
            return "";
        }

        private async void Button_SelectLog_Click(object sender, RoutedEventArgs e)
        {
            await UploadLog(false, GetSelectedLogText());
            LogUploaderFlyoutOpen = false;
        }

        class LogItem
        {
            public string filepath;
            public LogItem(string filepath)
            {
                this.filepath = filepath;
            }

            public override string ToString()
            {
                return System.IO.Path.GetFileName(filepath) + " - " + ByteSize.FromBytes(new FileInfo(filepath).Length);
            }
        }
    }
}
