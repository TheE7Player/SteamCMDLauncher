using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

using Newtonsoft.Json.Linq;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;

namespace SteamCMDLauncher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class Setup : Window
    {
        private bool first_time_run = true;

        private string selectedGame = String.Empty;
        private string steamcmd_location = String.Empty;
        private string folder_location = String.Empty;
        
        private int selectedGame_ID;
        private int[] available_IDS;

        public Setup(bool firstrun = true)
        {
            first_time_run = firstrun;

            InitializeComponent();

            ReturnBack.Visibility = (!first_time_run) ? Visibility.Visible : Visibility.Hidden;

            ToolTipService.SetShowOnDisabled(SteamCMDButton, true);

            if (Config.DatabaseExists)
            {
                var cmdLoc = Config.GetEntryByKey("cmd", Config.INFO_COLLECTION);

                steamcmd_location = (!(cmdLoc is null)) ? cmdLoc.AsString : String.Empty;

                SteamCMDButton.IsEnabled = String.IsNullOrEmpty(steamcmd_location);

                if (!SteamCMDButton.IsEnabled)
                { 
                    SteamCMDButton.ToolTip = new ToolTip { Content = $"Already set to: {steamcmd_location}", IsOpen = true };
                    Card1.IsEnabled = true;
                }

                ServerFolderButton.IsEnabled = !SteamCMDButton.IsEnabled;
            }

            this.GameDropDown.ItemsSource = GetSupportedGames();
        }

        private List<string> GetSupportedGames()
        {
            string file = System.Text.Encoding.Default.GetString(SteamCMDLauncher.Properties.Resources.dedicated_server_list);

            JObject objectA = JObject.Parse(file);
           
            file = null;

            available_IDS = objectA["server"]
                .Children()
                .Select(x => x.Value<int>("id")).ToArray();

            return objectA["server"]
                .Children()              
                .Select( x => x["game"].ToString() ).ToList();
        }

        private string GetFolder(string required_file, string rule_break)
        { 
            var dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog();
            dialog.InitialDirectory = "C:\\Users";
            dialog.IsFolderPicker = true;

            while (true)
            {
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    if(!string.IsNullOrEmpty(required_file))
                    if (!File.Exists(Path.Combine(dialog.FileName, required_file)))
                    {
                        MessageBox.Show(rule_break);
                        continue;
                    }
                    return dialog.FileName;
                } else { break; }
            }

            return string.Empty;
        }

        private void Log(string text) => System.Diagnostics.Debug.WriteLine(text);

        #region Events
        private void GameDropDown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Card2.IsEnabled = Card2.IsEnabled == false;
            
            // Assign the new ID and name of the current game to download for
            selectedGame = GameDropDown.SelectedValue.ToString();
            selectedGame_ID = available_IDS[GameDropDown.SelectedIndex];

            Log($"ID: {selectedGame_ID} | Game: {selectedGame}");
        }

        #region Button Events
        // "SteamCMD Location"
        private void SteamCMD_Click(object sender, RoutedEventArgs e)
        {

            steamcmd_location = GetFolder("steamcmd.exe", "The given path doesn't contain the 'steamcmd.exe' to install the game files! Try agin.");

            if (steamcmd_location.Length > 0)
            {
                Config.AddEntry_BJSON("cmd", steamcmd_location, Config.INFO_COLLECTION);

                Log(steamcmd_location);

                ServerFolderButton.IsEnabled = true;

                Card1.IsEnabled = Card1.IsEnabled == false;
            }
        }

        // Where to install
        private void Location_Click(object sender, RoutedEventArgs e)
        {
            folder_location = GetFolder(string.Empty, string.Empty);
            if (folder_location.Length > 0)
            {
                Config.AddEntry_BJSON("svr", folder_location, Config.INFO_COLLECTION);

                Log(folder_location);
                
                Card3.IsEnabled = Card3.IsEnabled == false;
            }             
        }

        private void InstallServer_Click(object sender, RoutedEventArgs e)
        {
            GameInstallName.Text = $"Now Installing:{Environment.NewLine}{selectedGame}";
            
            installDialog.IsOpen = true;

            Config.AddServer(selectedGame_ID, folder_location);
        }

        // Select file location for server (If installed already)
        private void ServerFolderButton_Click(object sender, RoutedEventArgs e)
        {

            folder_location = GetFolder("steam_appid.txt", "Need to locate the game dir where 'steam_appid.txt' is located, if not there please enforce it manually");
            if (folder_location.Length > 0)
            {
                // Validate if the id is correct

                string appid_loc = File.ReadAllText(Path.Combine(folder_location, "steam_appid.txt")).Trim();

                int result;
                if(!Int32.TryParse(appid_loc, out result))
                {
                    MessageBox.Show($"Unable to support (from parsing) from id '{appid_loc}' - some features may be not implemented or available");
                    return;
                }

                if(!available_IDS.Any(x => x == result))
                {
                    MessageBox.Show($"Unable to support from id '{appid_loc}' - some features may be not implemented or available");
                    return;
                }

                //Config.AddEntry_BJSON("svr", folder_location, Config.INFO_COLLECTION);
                Config.AddServer(result, folder_location);

                Log(folder_location);
            }
        }
        
        // Return back to home screen (if not first time/setup)
        private void ReturnBack_Click(object sender, RoutedEventArgs e)
        {
            var main = new main_view();
            this.Close();
            main.Show();
        }
        #endregion

        #endregion

    }
}
