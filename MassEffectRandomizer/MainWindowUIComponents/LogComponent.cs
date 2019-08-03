using ByteSizeLib;
using Flurl.Http;
using MahApps.Metro;
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
using System.Text;
using System.Threading;
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


        public string GetSelectedLogText(string logpath)
        {
            string temppath = logpath + ".tmp";
            File.Copy(logpath, temppath);
            string log = File.ReadAllText(temppath);
            File.Delete(temppath);

            string eventAndCrashLogs = GetLogsForAppending();
            return eventAndCrashLogs + "\n\n" + log;
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

        private void Button_CancelLog_Click(object sender, RoutedEventArgs e)
        {
            LogUploaderFlyoutOpen = false;
        }

        private async Task<string> UploadLog(bool isPreviousCrashLog, string logfile, bool openPageWhenFinished = true)
        {
            BackgroundWorker bw = new BackgroundWorker();
            string outfile = Path.Combine(Utilities.GetAppDataFolder(), "logfile_forUpload.lzma");
            byte[] lzmalog = null;
            string randomizerVer = System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString();
            ProgressDialogController progresscontroller = await this.ShowProgressAsync("Collecting logs", "Mass Effect Randomizer is currently collecting log information, please wait...", true);
            progresscontroller.SetIndeterminate();

            bw.DoWork += (a, b) => {
                Log.Information("Collecting log information...");
                string log = GetSelectedLogText(logfile);
                //var lzmaExtractedPath = Path.Combine(Path.GetTempPath(), "lzma.exe");

                ////Extract LZMA so we can compress log for upload
                //using (Stream stream = Utilities.GetResourceStream("MassEffectRandomizer.staticfiles.lzma.exe"))
                //{
                //    using (var file = new FileStream(lzmaExtractedPath, FileMode.Create, FileAccess.Write))
                //    {
                //        stream.CopyTo(file);
                //    }
                //}

                var lzmaExtractedPath = Utilities.ExtractInternalStaticExecutable("lzma.exe", false);


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
                        return;
                    }
                }

                string zipStaged = Path.Combine(Utilities.GetAppDataFolder(), "logfile_forUpload");
                File.WriteAllText(zipStaged, log);

                //Compress with LZMA for VPS Upload
                string args = "e \"" + zipStaged + "\" \"" + outfile + "\" -mt2";
                Utilities.runProcess(lzmaExtractedPath, args);
                File.Delete(zipStaged);
                File.Delete(lzmaExtractedPath);
                lzmalog = File.ReadAllBytes(outfile);
                File.Delete(outfile);
                Log.Information("Finishing log collection thread");
            };

            bw.RunWorkerCompleted += async (a, b) =>
            {
                progresscontroller.SetTitle("Uploading log");
                progresscontroller.SetMessage("Uploading log to ME3Tweaks log viewer, please wait...");
                try
                {
                    var responseString = await "https://vps.me3tweaks.com/masseffectrandomizer/logupload.php".PostUrlEncodedAsync(new {LogData = Convert.ToBase64String(lzmalog), MassEffectRandomizerVersion = randomizerVer, Type = "log", CrashLog = isPreviousCrashLog}).ReceiveString();
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
                Log.Information("Finishing log upload");
                LogUploaderFlyoutOpen = false;
                
            };
            bw.RunWorkerAsync();
            return ""; //Async requires this
        }
        

        private async void Button_SelectLog_Click(object sender, RoutedEventArgs e)
        {
            await UploadLog(false, ((LogItem)Combobox_LogSelector.SelectedValue).filepath);
        }

        private string GetLogsForAppending()
        {
            //GET LOGS
            StringBuilder crashLogs = new StringBuilder();
            string logsdir = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\BioWare\Mass Effect\Logs";
            if (Directory.Exists(logsdir))
            {
                DirectoryInfo info = new DirectoryInfo(logsdir);
                FileInfo[] files = info.GetFiles().Where(f => f.LastWriteTime > DateTime.Now.AddDays(-7)).OrderByDescending(p => p.LastWriteTime).ToArray();
                DateTime threeDaysAgo = DateTime.Now.AddDays(-3);
                Console.WriteLine("---");
                foreach (FileInfo file in files)
                {
                    Console.WriteLine(file.Name + " " + file.LastWriteTime);
                    var logLines = File.ReadAllLines(file.FullName);
                    int crashIndex = -1;
                    int index = 0;
                    string reason = "";
                    foreach (string line in logLines)
                    {
                        if (line.Contains("Critical: appError called"))
                        {
                            crashIndex = index;
                            reason = "Log file indicates crash occured";
                            Log.Information("Found crash in ME1 log " + file.Name + " on line " + index);
                            break;
                        }

                        if (line.Contains("Uninitialized: Log file closed"))
                        {
                            crashIndex = index;
                            reason = "~~~Standard log file";
                            Log.Information("Found standard log file " + file.Name + " on line " + index);
                            break;
                        }

                        index++;
                    }

                    if (crashIndex >= 0)
                    {
                        crashIndex = Math.Max(0, crashIndex - 10);
                        //this log has a crash
                        crashLogs.AppendLine("===Mass Effect log " + file.Name);
                        if (reason != "") crashLogs.AppendLine(reason);
                        if (crashIndex > 0)
                        {
                            crashLogs.AppendLine("[CRASHLOG]...");
                        }

                        for (int i = crashIndex; i < logLines.Length; i++)
                        {
                            crashLogs.AppendLine("[CRASHLOG]" + logLines[i]);
                        }
                    }
                }
            }

            //Get event logs
            EventLog ev = new EventLog("Application");
            List<string> entries = ev.Entries
                .Cast<EventLogEntry>()
                .Where(z => z.InstanceId == 1001)
                .Select(GenerateLogString)
                .Where(x => x.Contains("MassEffect.exe"))
                .ToList();

            crashLogs.AppendLine("Mass Effect crash logs found in Event Viewer");
            if (entries.Count > 0)
            {
                entries.ForEach(x=>crashLogs.AppendLine(x));
            }
            else
            {
                crashLogs.AppendLine("No crash events found in Event Viewer");
            }

            return crashLogs.ToString();
        }

        public string GenerateLogString(EventLogEntry CurrentEntry) => $"====================================================================================\nEvent type: {CurrentEntry.EntryType.ToString()}\nEvent Message: {CurrentEntry.Message + CurrentEntry}\nEvent Time: {CurrentEntry.TimeGenerated.ToShortTimeString()}\nEvent---------------------------------------------------- {CurrentEntry.UserName}\n";
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
