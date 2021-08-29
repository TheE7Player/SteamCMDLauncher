using System;
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
            // TODO: Make GHU-C have a action to callback faults on error

            string repo_link = "repos/TheE7Player/SteamCMDLauncher";
            GitHubUpdaterCore.GitHubFunctions udp;
            GitHubUpdaterCore.Repo repo;
            bool needs_update = false;

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
            }        
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {

            StartTime = DateTime.Now;

            // Setting up exception handlers
            Current.DispatcherUnhandledException += new DispatcherUnhandledExceptionEventHandler(AppDispatcherUnhandledException);

            string file = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "stderr");

            if (!System.IO.Directory.Exists(file))
                System.IO.Directory.CreateDirectory(file);

            file = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

            if (!System.IO.Directory.Exists(file))
                System.IO.Directory.CreateDirectory(file);

            file = null;

#if RELEASE
            // Messagebox only shows IF the compiling is set to "RELEASE" / Production mode 
            MessageBox.Show("This program is in alpha stage and doesn't contain an updater - Keep up to date from GitHub or Discord Channel.");
#endif

            // Code for before window opens (optional);
            Cleanup();

            // Perform any updates
            if (DoUpdate())
                MessageBox.Show("Update is required");

            Window mainWindow;

            if (!Config.DatabaseExists || !Config.HasServers())
                mainWindow = new Setup();
            else 
                mainWindow = new main_view();

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
