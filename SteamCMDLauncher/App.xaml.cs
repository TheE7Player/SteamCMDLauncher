using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SteamCMDLauncher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Cleanup()
        {
            Window extra_min = new Views.extra();

            if(Keyboard.IsKeyDown(Key.RightShift))
            {
                Console.Beep();
                //System.Threading.Thread.Sleep(1000);

                extra_min.ShowDialog();
            }
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
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

            mainWindow.Show(); mainWindow.Focus();
            mainWindow.Closed += Window_Closed;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            //Console.Beep();
            Environment.Exit(0);
        }
    }
}
