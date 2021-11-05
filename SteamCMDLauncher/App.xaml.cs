using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Diagnostics;

// [NOTE] DON'T REMOVE THIS - It's not used in DEBUG mode but RELEASE mode!
using System.Linq;
using System.Threading.Tasks;

namespace SteamCMDLauncher
{

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {     
        #region Attributes
        /// <summary>
        /// If the program should exit if an close event is triggered
        /// </summary>
        public static bool CancelClose = false;

        /// <summary>
        /// Holds the current version of the program
        /// </summary>
        public static string _version = "0.7.2";

        /// <summary>
        /// Holds the string to display the version of the program
        /// </summary>
        public static string Version = $"Version {_version}";

        /// <summary>
        /// A flag which is enabled if the program running isn't the latest available
        /// </summary>
        private bool needsUpdate = false;

        /// <summary>
        /// Holds the time the program first starts (Used for exceptions)
        /// </summary>
        public static DateTime StartTime;

        private static Window ActiveWindow;

        // FOR WINDOWS: NotifyIcon for when window minimizes 
        private static System.Windows.Forms.NotifyIcon NotifyIcon;
        #endregion

        #region Methods
        /// <summary>
        /// Handles cleaning window logic (Holding RSHIFT when booting)
        /// </summary>
        private void Cleanup()
        {
            // If the RSHIFT is held down while booting...
            if(Keyboard.IsKeyDown(Key.RightShift))
            {
                // Make a console beep (to let user know its acknowledged)
                Console.Beep();

                // Then show the window as a dialog (halts until close event is fired)
                WindowOpen(new Views.extra(), true);               
            }
        }

        /// <summary>
        /// Updater logic to check if running latest version
        /// </summary>
        /// <returns>True if the program needs to be updated</returns>
        private bool DoUpdate()
        {
            // TODO: [?] Make GHU-C have a action to callback faults on error

            if (!Component.Win32API.IsConnectedToInternet())
            {
                Config.Log("[APP] [!] Unable to perform program update check due to no Internet access available. [!] ");
                return false;
            }

            // The UTC format to convert to: "2021-08-30T17:00:49Z" <- Example of what it looks like
            string utc_format = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'";

            // The path where the file will be placed, near the exe folder (/runtimes/.update_check)
            string update_path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "runtimes",
                ".update_check");

            // The GitHub repo link of where the project is held
            string repo_link = "repos/TheE7Player/SteamCMDLauncher";
            
            // 2 booleans which handles the update logic
            bool latest_version = false, requires_check = true;
 
            // Validate path first
            if (File.Exists(update_path))
            {
                // An existing '.update_check' exists, let's parse it and see when it was last checked

                // Create a nullable DateTime object, where we store the file date from
                DateTime? fileDate = null;
                
                // File already exists, lets evaluate it
                using (FileStream fs = File.OpenRead(update_path))
                {
                    // Validate if its an valid UTC length, which is 24 characters long
                    if (fs.Length != 24) throw new Exception("Updater reading file is corrupted, Length not equal to 24 bytes!");

                    // Create a byte array to hold the buffer of the date from the file
                    byte[] date_buffer = new byte[24];
                    
                    // Create a UTF8Encoding object, to turn the bytes into a string
                    UTF8Encoding utf_c = new UTF8Encoding(true);
                    
                    // Read the file stream, write it to the buffer with its selected length ( "> 0" to validate if its successful to read )
                    while (fs.Read(date_buffer, 0, date_buffer.Length) > 0)
                    {
                        // Put the read data into the 'fileDate' object
                        fileDate = DateTime.ParseExact(utf_c.GetString(date_buffer), utc_format, System.Globalization.CultureInfo.InvariantCulture);                      
                    }

                    // Then deference the objects as we don't require them any longer
                    utf_c = null;
                    date_buffer = null;
                }

                // If the 'fileDate' object is still not assigned, throw an error stating it failed.
                if (fileDate is null) throw new Exception("Updater DateTime parse failed, the assigned result was still left blank.");

                // Fix the time - correct date but hours are wrong
                fileDate = TimeZoneInfo.ConvertTimeFromUtc((DateTime)fileDate, TimeZoneInfo.Local);

                // Now, we cast the object into a 'TimeSpan' to get the total minutes from the last time of the check
                requires_check = ((TimeSpan)(DateTime.Now - fileDate)).TotalMinutes > 30;
            }
            
            // [!] Check if the '.due_update' file exists, this takes priority over if a check is even needed [!]
            
            // If an updater file exists, update it
            if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtimes", ".due_update"))) return true;

            // If the result is still false, we can just ignore the check as it has been done.
            if (!requires_check) return false;

            // Write the new date as the updater will now check if update is available
            if(File.Exists(update_path))
                File.SetAttributes(update_path, FileAttributes.Normal);
            
            // Write the file and then hide it (visible if you enable "show hidden files")
            File.WriteAllText(update_path, DateTime.Now.ToUniversalTime().ToString(utc_format));
            File.SetAttributes(update_path, FileAttributes.Hidden);

            // Create the objects from "GitHubUpdater" repo
            // [NOTE]: This is the .Net Core 3.1 edition of the project, not available for public (yet).
            GitHubUpdaterCore.GitHubFunctions udp;
            GitHubUpdaterCore.Repo repo;

            try
            {
                Config.Log($"Attempting to fetch Repo from GitHub link: {repo_link}");
                
                // Setup the object to get the latest details from the project online
                udp = new GitHubUpdaterCore.GitHubFunctions(repo_link, GitHubUpdaterCore.GitHubUpdater.LogTypeSettings.LogWithError, true);

                //Since DirectSearch targets one project, use .GetRepostiory(0);
                repo = udp.GetRepository(0);

                if (repo != null) //Ensure it isn't null before fetching information
                {
                    Config.Log("Now, comparing version from online to running version...");

                    // Use the 'ProductComparer' to see if the version is less than the latest version possible
                    latest_version = GitHubUpdaterCore.ProductComparer.CompareVersionLess("thee7player", _version, repo);

                    if (!latest_version)
                        Config.Log($"[!] Your running on version {_version}, which is out of date! [!]");
                }
                else
                {
                    // Throw an error, as this should have worked
                    throw new Exception($"Couldn't find github page based on link: {repo_link}! Check if link is working or if connected to internet!");
                }

                Config.Log($"Fetch complete with {udp.getAPIFetchCount()} calls remaining");

                // We perform the opposite as 'true' means it needs an update
                return !latest_version;
            }
            catch (Exception ex)
            {
                // Throw an error, likely from GitHubUpdater library
                throw ex;
            }
            finally
            {
                // Deference the reference objects, as we don't need them in memory any more
                repo_link = null;
                udp = null;
                repo = null;
                update_path = null;
                utc_format = null;
            }
        }

        private Process[] SameProcesses()
        {
            Process self = Process.GetCurrentProcess();
            
            Span<Process> targets = Process.GetProcessesByName(self.ProcessName).AsSpan();

            int slice_start = 0;

            for (int i = 0; i < targets.Length; i++)
            {
                if (self.Id != targets[i].Id) { slice_start++; } else { break; }
            }

            self = null;

            return targets.Slice(0, slice_start).ToArray();
        }
        #endregion

        #region Window Logic
        /// <summary>
        /// The EntryPoint to the program
        /// </summary>
        private void App_Startup(object sender, StartupEventArgs e)
        {
            // Disable Hardware Acceleration (If not supported by GPU or CPU)

            if (System.Windows.Media.RenderCapability.Tier >> 16 == 0)
            {
                Config.Log("[RENDERER] GPU Tier 0 - Forcing Software Renderer as Hardware Render isn't possible");
                System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
            } 
            else
            {
                Config.Log("[RENDERER] GPU Tier 1/2 - Hardware Render is supported by default, Ignore");
            }
            
            #if RELEASE
            // Clear any old logs (if any)
            FileInfo[] old_log_files = Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"))
                .Select(x => new FileInfo(x))
                .ToArray();

            // Clear any logs that were found to be more than a day old
            if(old_log_files?.Length > 0)
            {
                DateTime expired_date = DateTime.Now.AddDays(-1);

                int deleteLen = old_log_files.Length;
                for (int i = 0; i < deleteLen; i++)
                {
                    if(old_log_files[i].LastWriteTime < expired_date)
                        old_log_files[i].Delete();
                }
            }

            old_log_files = null;
            #endif

            // Set the 'StartTime' to the current date
            StartTime = DateTime.Now;

            Config.Log("Application is launched");

            Config.Log("Checking for any same running applications");

            Process[] other_app = SameProcesses();

            if (other_app?.Length == 0)
            {
                Config.Log("No other running instances, good to go!");
            }
            else
            {
                int size = other_app.Length;
                for (int i = 0; i < size; i++)
                {
                    Config.Log($"Found redundant process of itself at ID {other_app[i].Id}... prompting a kill...");
                    other_app[i].Kill();

                    if(other_app[i].WaitForExit(2000))
                    {
                        if (!other_app[i].HasExited)
                        {
                            Config.Log($"Redundant process of {other_app[i].Id} didn't close down by itself!");
                            MessageBox.Show($"Another instance of ID {other_app[i].Id} is running - Please close it down before running again! Exiting...");
                            return;
                        }
                    }
                }
            }

            // Setting up exception handlers
            Current.DispatcherUnhandledException += new DispatcherUnhandledExceptionEventHandler(AppDispatcherUnhandledException);

            // Create the folders if they aren't created yet

            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "stderr");

            if (!Directory.Exists(file))
                Directory.CreateDirectory(file);

            file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

            if (!Directory.Exists(file))
                Directory.CreateDirectory(file);

            file = null;

#if RELEASE
            // [NOTE]: Updater only runs when program is built on "RELEASE" mode

            // Store the path location of where the '.due_update' file should be created
            string update_file_loc = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtimes", ".due_update");
            
            // Check if the file exists from the last update check
            bool updateFileExists = File.Exists(update_file_loc);
            
            // Start the logic to see the last check
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

            // Finally, dereference the string for GCC
            update_file_loc = null;
#endif
            // Code for before window opens (optional);
            Cleanup();

            if (!Config.DatabaseExists || !Config.HasServers())
                WindowOpen(new Setup());
            else
                WindowOpen(new main_view(needsUpdate));
        }

        public static void WindowOpen(Window instance, bool AsDialog = false)
        {
            string current_instance = instance.DependencyObjectType.Name;
            
            Config.Log($"[WO] Current main window instance is: {current_instance}.xaml ({ActiveWindow?.DependencyObjectType.Name} -> {current_instance})");
                   
            if (ActiveWindow != null) { ActiveWindow = null; }

            if (NotifyIcon is null)
            {
                NotifyIcon = new System.Windows.Forms.NotifyIcon();
                NotifyIcon.Icon = new System.Drawing.Icon("icon.ico");
                NotifyIcon.Visible = false;
                NotifyIcon.Click += NotifyClick;
                NotifyIcon.BalloonTipClicked += NotifyClick;
            }

            ActiveWindow = instance;
            ActiveWindow.StateChanged += AsyncToggleNotifyState;

            instance = null;
            current_instance = null;

            if (!AsDialog)
            { ActiveWindow.Show(); }
            else
            { ActiveWindow.ShowDialog(); }
        }
        
        private static void NotifyClick(object sender, EventArgs e)
        {
            sender = null; e = null;

            NotifyIcon.Visible = false;
            ActiveWindow.ShowInTaskbar = true;

            Component.Win32API.ForceWindowOpen(ref ActiveWindow);
        }

        public static async Task ForceNotify(string title, string message, System.Windows.Forms.ToolTipIcon icon, int delay, bool condition, bool IconVisibility, bool TaskBarVisibility)
        {
            await System.Threading.Tasks.Task.Delay(200);

            NotifyIcon.Visible = IconVisibility;
            ActiveWindow.ShowInTaskbar = TaskBarVisibility;

            if(condition) NotifyIcon.ShowBalloonTip(delay, title, message, icon);

            title = null;
            message = null;
        }

        private static async void AsyncToggleNotifyState(object sender, EventArgs e)
        {
            sender = null; e = null;

            bool cond1 = ActiveWindow.WindowState == WindowState.Minimized;
            bool cond2 = ActiveWindow.WindowState == WindowState.Normal;

            await ForceNotify("Window Hidden",
                "Click here to resume the window",
                System.Windows.Forms.ToolTipIcon.Info,
                250,
                cond1, cond1, cond2);
        }

        public static WeakReference GetActiveWindow() => new WeakReference(App.ActiveWindow);

        public static void WindowClosed(Window sender)
        {
            string window = sender.DependencyObjectType.Name;

            ActiveWindow.StateChanged -= AsyncToggleNotifyState;

            Config.Log($"[EXIT EVENT] Cancel request was requested from window: {window}.xaml ({ActiveWindow?.DependencyObjectType.Name} -> {window})");
            
            // Exit the program entirely if it should do (no depending tasks to be done)
            if (!CancelClose)
            {
                Config.Log($"[EXIT EVENT] Cancel request was granted from window: {window}.xaml");
                NotifyIcon.Visible = false;
                NotifyIcon.Icon = null;
                NotifyIcon.Dispose();
                NotifyIcon = null;
                Environment.Exit(0);
            }
            else
            {
                Config.Log($"[EXIT EVENT] Cancel request was rejected from window: {window}.xaml");
                ActiveWindow?.Hide();
            }

            window = null;
            sender = null;
        }
#endregion

        #region Exception Handling
        void AppDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Show dialog if RELEASE only
#if RELEASE
            ShowUnhandledException(e);
#endif
        }

        void ShowUnhandledException(DispatcherUnhandledExceptionEventArgs e)
        {
            // Till the compiler we are dealing with this exception ourself
            e.Handled = true;

            CancelClose = true;

            // Close this window
            WindowClosed(MainWindow);

            // Create the exception window, and pass in the exception that got raised           
            // Show the exception dialog and halt for an close event
            WindowOpen(new Views.exception(e.Exception), true);

            // Exit the program entirely
            Environment.Exit(0);
        }
        #endregion
    }
}
