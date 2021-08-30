using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SteamCMDLauncher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static bool CancelClose = false;

        public static string _version = "0.5";
        public static string Version = $"Version {_version}";

        private bool needsUpdate = false;

        public static DateTime StartTime;

        private void Cleanup()
        {
            Window extra_min = new Views.extra();

            if(Keyboard.IsKeyDown(Key.RightShift))
            {
                Console.Beep();

                extra_min.ShowDialog();
            }

            extra_min = null;
        }

        private bool DoUpdate()
        {
            // TODO: [?] Make GHU-C have a action to callback faults on error

            string utc_format = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'";
            string update_path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "runtimes",
                ".update_check");
            string repo_link = "repos/TheE7Player/SteamCMDLauncher";
            
            bool needs_update = false;
            bool requires_check = true;

            // Validate path
            if (File.Exists(update_path))
            {
                DateTime? fileDate = null;
                
                // File already exists, lets evaluate it
                using (FileStream fs = File.OpenRead(update_path))
                {

                    if (fs.Length > 24) throw new Exception("Updater reading file is corrupted, Length was more than 24 bytes!");

                    byte[] b = new byte[24];
                    
                    UTF8Encoding temp = new UTF8Encoding(true);
                    
                    while (fs.Read(b, 0, b.Length) > 0)
                    {
                        fileDate = DateTime.ParseExact(temp.GetString(b), utc_format, System.Globalization.CultureInfo.InvariantCulture);                      
                    }

                    temp = null;
                    b = null;
                }

                if (fileDate is null) throw new Exception("Updater DateTime parse failed, the assigned result was still left blank.");

                // Fix the time - correct date but hours are wrong
                fileDate = TimeZoneInfo.ConvertTimeFromUtc((DateTime)fileDate, TimeZoneInfo.Local);

                requires_check = ((TimeSpan)(DateTime.Now - fileDate)).TotalMinutes > 30;
            }
            
            // [!] Check if the '.due_update' file exists, this takes priority over if a check is even needed [!]
            
            // If an updater file exists, update it
            if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtimes", ".due_update"))) return true;

            if (!requires_check) return false;

            // Write the new date as the updater will now check if update is available
            if(File.Exists(update_path))
                File.SetAttributes(update_path, FileAttributes.Normal);
            
            File.WriteAllText(update_path, DateTime.Now.ToUniversalTime().ToString(utc_format));
            File.SetAttributes(update_path, FileAttributes.Hidden);

            GitHubUpdaterCore.GitHubFunctions udp;
            GitHubUpdaterCore.Repo repo;

            try
            {
                Config.Log($"Attempting to fetch Repo from GitHub link: {repo_link}");
                udp = new GitHubUpdaterCore.GitHubFunctions(repo_link, GitHubUpdaterCore.GitHubUpdater.LogTypeSettings.LogWithError, true);

                //Since DirectSearch targets one project, use .GetRepostiory(0);
                repo = udp.GetRepository(0);

                if (repo != null) //Ensure it isn't null before fetching information
                {
                    Config.Log("Now, comparing version from online to running version...");
                    needs_update = GitHubUpdaterCore.ProductComparer.CompareVersionLess("thee7player", _version, repo);

                    if (needs_update)
                        Config.Log($"[!] Your running on version {_version}, which is out of date! [!]");
                }
                else
                {
                    throw new Exception($"Couldn't find github page based on link: {repo_link}! Check if link is working or if connected to internet!");
                }

                Config.Log($"Fetch complete with {udp.getAPIFetchCount()} calls remaining");

                return needs_update;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                repo_link = null;
                udp = null;
                repo = null;
                update_path = null;
                utc_format = null;
            }        
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {

            StartTime = DateTime.Now;

            Config.Log("Application is launched");

            // Setting up exception handlers
            Current.DispatcherUnhandledException += new DispatcherUnhandledExceptionEventHandler(AppDispatcherUnhandledException);

            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "stderr");

            if (!Directory.Exists(file))
                Directory.CreateDirectory(file);

            file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

            if (!Directory.Exists(file))
                Directory.CreateDirectory(file);

            file = null;

#if RELEASE
            string update_file_loc = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtimes", ".due_update");
            bool updateFileExists = File.Exists(update_file_loc);
            bool requiresUpdate = DoUpdate();
            
            // Perform any updates        
            if (requiresUpdate)
            {
                needsUpdate = true;
                
                // Create the file if not already
                if (!updateFileExists)
                {
                    File.WriteAllText(update_file_loc, "1");
                    File.SetAttributes(update_file_loc, FileAttributes.Hidden);
                }
            } 
            else
            {
                // Remove the file as an update is done or is none
                if (updateFileExists && !requiresUpdate) 
                {
                    File.SetAttributes(update_file_loc, FileAttributes.Normal);
                    File.Delete(update_file_loc);
                }
            }
            update_file_loc = null;
#endif
            // Code for before window opens (optional);
            Cleanup();

            Window mainWindow;

            if (!Config.DatabaseExists || !Config.HasServers())
                mainWindow = new Setup();
            else
                mainWindow = new main_view(needsUpdate);

            mainWindow.Show();
            mainWindow.Focus();
            mainWindow.Closed += Window_Closed;
        }

        void AppDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Show dialog if RELEASE only
            #if RELEASE

            ShowUnhandledException(e);    

            #endif
        }

        void ShowUnhandledException(DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            Views.exception win = new Views.exception(e.Exception);

            App.Current.MainWindow.Close();

            win.ShowDialog();

            Environment.Exit(0);

        }

        public static void Window_Closed(object sender, EventArgs e)
        {
            if (!CancelClose)
            { Environment.Exit(0); }
        }
    }
}
