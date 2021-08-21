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

        public static string Version = "Version 0.3";

        public static DateTime StartTime;

        private void Cleanup()
        {
            Window extra_min = new Views.extra();

            if(Keyboard.IsKeyDown(Key.RightShift))
            {
                Console.Beep();

                extra_min.ShowDialog();
            }
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {

            StartTime = DateTime.Now;

            // Setting up exception handlers
            Application.Current.DispatcherUnhandledException += new DispatcherUnhandledExceptionEventHandler(AppDispatcherUnhandledException);

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
