using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

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

            App.CancelClose = true;

            InitializeComponent();

            ReturnBack.Visibility = (!first_time_run) ? Visibility.Visible : Visibility.Hidden;

            ToolTipService.SetShowOnDisabled(SteamCMDButton, true);

            if (Config.DatabaseExists)
            {
                var cmdLoc = Config.GetEntryByKey("cmd", Config.INFO_COLLECTION);

                steamcmd_location = (!(cmdLoc is null)) ? cmdLoc.AsString : string.Empty;

                SteamCMDButton.IsEnabled = string.IsNullOrEmpty(steamcmd_location);

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
                .Select(x => x["game"].ToString()).ToList();
        }

        private string Show_AG_Dialog(string[] games)
        {
            // Holds the current game it may have found
            string current_game = string.Empty;
            
            string game_id = string.Empty;

            // Holds the logic to show and change the dialog
            UIComponents.DialogHostContent confirmDialog = new UIComponents.DialogHostContent(RootDialog);
                  
            // Holds the current found number (iterator)
            int count = 1;
           
            // Holds the max found games (cached)
            int max = games.Length;

            // Loop through each game that was found
            foreach (var game in games)
            {
                // Get the game name based on the 'appid' given
                current_game = Config.GetGameByAppId(game);

                // Change the dialog to reflect if that was the game that was found
                confirmDialog.YesNoDialog($"Found Game {count++} of {max}", $"Is the game your adding '{current_game}'?");

                // Show the dialog
                confirmDialog.ShowDialog();

                // Keep going if the result is pending (-1) or is false (0) - Do this until one is true (if)
                if (confirmDialog.GetResult() == 1) { game_id = game; break; }

            }

            confirmDialog.Destory();
           
            confirmDialog = null;
            
            current_game = null;

            return game_id;
        }

        #region Events
        private void GameDropDown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(!Card2.IsEnabled)
                Card2.IsEnabled = true;
            
            // Assign the new ID and name of the current game to download for
            selectedGame = GameDropDown.SelectedValue.ToString();
            selectedGame_ID = available_IDS[GameDropDown.SelectedIndex];

            Config.Log($"ID: {selectedGame_ID} | Game: {selectedGame}");
        }

        #region Button Events
        // "SteamCMD Location"
        private void SteamCMD_Click(object sender, RoutedEventArgs e)
        {        
            steamcmd_location = Config.GetFolder("steamcmd.exe", new Action(() =>
            {
                var ui = new UIComponents.DialogHostContent(RootDialog);
                ui.OKDialog("The given path doesn't contain the 'steamcmd.exe' to install the game files! Try again.");
                ui.ShowDialog();
                this.Hide();
            }));
            
            this.Show();

            if (steamcmd_location.Length > 0)
            {
                Config.AddEntry_BJSON("cmd", steamcmd_location, Config.INFO_COLLECTION);

                Config.Log(steamcmd_location);

                ServerFolderButton.IsEnabled = true;

                if (!Card1.IsEnabled)
                    Card1.IsEnabled = true;
            }
        }

        // Where to install
        private void Location_Click(object sender, RoutedEventArgs e)
        {
            folder_location = Config.GetFolder(string.Empty, string.Empty);
            if (folder_location.Length > 0)
            {
                Config.Log(folder_location);

                if (!Card3.IsEnabled)
                    Card3.IsEnabled = true;
            }
        }

        private void InstallServer_Click(object sender, RoutedEventArgs e)
        {

            UIComponents.DialogHostContent installDialog = new UIComponents.DialogHostContent(RootDialog, false);

            installDialog.GameInstallDialog(selectedGame);

            installDialog.ChangePropertyText("GameInstallStatus", "Pre-running 'steamexe.exe' - waiting for result");

            Component.SteamCMD cmd = new Component.SteamCMD(steamcmd_location);

            // Force all the buttons to be inactive
            SteamCMDButton.IsHitTestVisible = false;
            ServerFolderButton.IsHitTestVisible = false;
            ReturnBack.IsHitTestVisible = false;
            SelectDirFolder.IsHitTestVisible = false;

            // Extra validation if the ID is 90 due to multiple games sharing them

            // For the json, they increment to prevent duplicates
            if (selectedGame_ID >= 90 && selectedGame_ID <= 99)
            {
                switch (selectedGame)
                {
                    case "Counter-Strike: Condition Zero": cmd.AddArgument("+app_set_config \"90 mod czero\""); break;
                    case "Day of Defeat": cmd.AddArgument("+app_set_config \"90 mod dod\""); break;
                    case "Deathmatch Classic": cmd.AddArgument("+app_set_config \"90 mod dmc\""); break;
                    case "Ricochet": cmd.AddArgument("+app_set_config \"90 mod ricochet\""); break;
                    case "Team Fortress Classic": cmd.AddArgument("	+app_set_config \"90 mod tfc\""); break;
                    case "Half-Life: Opposing Force": cmd.AddArgument("+app_set_config \"90 mod gearbox\""); break;
                }
            }

            main_view main_win = new main_view();

            installDialog.ShowDialog();

            Task _ = Task.Run(async () =>
            {
                cmd.PreRun();

                await this.Dispatcher.Invoke(async () =>
                {
                    installDialog.ChangePropertyText("GameInstallStatus", "Installing... Don't close this window");

                    await Task.Delay(3000);

                    cmd.InstallGame(selectedGame_ID, folder_location);

                    installDialog.ChangePropertyText("GameInstallStatus", "Installed! Return back to main page.");

                    await Task.Delay(100);
                    
                    Config.AddServer(selectedGame_ID, folder_location);

                    await Task.Delay(2100);

                    installDialog.CloseDialog();

                    await Task.Delay(2100);

                    installDialog.Destory();

                    main_win.Show();

                    this.Close();
                });
            });
        }

        // Select file location for server (If installed already)
        private void ServerFolderButton_Click(object sender, RoutedEventArgs e)
        {
            folder_location = Config.GetFolder(null, "");

            if (folder_location.Length > 0)
            {
                // Validate if the id is correct
                var found_games = Config.FindGameID(folder_location);
                string appid_loc = string.Empty;

                // Creating the programmatic version of a dialog host (using MatieralDesigns)
                if(found_games.Length > 0)
                {
                    string found_game = Show_AG_Dialog(found_games);

                    if (!string.IsNullOrEmpty(found_game))
                        appid_loc = found_game;
                }

                var dialog = new UIComponents.DialogHostContent(RootDialog);

                if(string.IsNullOrEmpty(appid_loc))
                {
                    dialog.OKDialog("Sorry but the program didn't manage to find the appid based on current folders\nPlease help by stating what commands and files to run that in this program.");
                    dialog.ShowDialog();
                    return;
                }

                int result;
                if(!Int32.TryParse(appid_loc, out result))
                {
                    dialog.OKDialog($"Unable to support (from parsing) from id '{appid_loc}' - some features may be not implemented or available");
                    dialog.ShowDialog();
                    return;
                }

                if(!available_IDS.Any(x => x == result))
                {
                    dialog.OKDialog($"Unable to support from id '{appid_loc}' - some features may be not implemented or available");
                    dialog.ShowDialog();
                    return;
                }

                Config.AddServer(result, folder_location);

                Config.Log(folder_location);

                dialog.OKDialog("Server has been added - Returning to home screen");
                dialog.ShowDialog();

                var mw = new main_view();
                mw.Show();
                this.Close();
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
