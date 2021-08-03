﻿using System;
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
            if(Keyboard.IsKeyDown(Key.RightShift))
            {
                Console.Beep();
                System.Threading.Thread.Sleep(1000);
                int clear_db_count = 1;

                if(Keyboard.IsKeyDown(Key.RightShift) && Keyboard.IsKeyDown(Key.L))
                {
                    Config.Log("Clearing logs...");
                    MessageBox.Show("Clearing log shortcut acknowledged - Performing cleaning");

                    if(Config.CleanLog())
                        MessageBox.Show("Clearing log was successful");
                    else
                        MessageBox.Show("Clearing log was unsuccessful or there were no logs to delete.");
                    
                    return;
                }

                while(Keyboard.IsKeyDown(Key.RightShift))
                {
                    clear_db_count++;
                    System.Threading.Thread.Sleep(500);

                    if (clear_db_count < 2)
                    { Console.Beep(); }
                    else if (clear_db_count == 3)
                    { Console.Beep(); System.Threading.Thread.Sleep(1); Console.Beep(); break; }
                }

                if (clear_db_count == 3)
                {
                    if (Config.DatabaseExists)
                    {
                        System.IO.File.Delete(Config.DatabaseLocation);
                        MessageBox.Show("Database was been erased - no trace was left. Starting brand new.");
                    } else
                    {
                        MessageBox.Show("Database wasn't erased - as there were no database to begin with.");
                    }
                }
            }
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            // Code for before window opens (optional);
            Cleanup();

            Window mainWindow;
            if (!Config.DatabaseExists && !Config.HasServers()) 
                mainWindow = new Setup(); 
            else 
                mainWindow = new main_view();

            mainWindow.Show(); mainWindow.Focus();
            //mainWindow.Closed += Window_Closed;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // Code for after window closes goes here.
            //MessageBox.Show("Goodbye World!");
        }
    }
}
