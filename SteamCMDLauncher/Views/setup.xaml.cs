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
        UIComponents.DialogHostContent dh;
        private bool first_time_run = true;

        private string selectedGame = string.Empty;
        private string steamcmd_location = string.Empty;
        private string folder_location = string.Empty;
        
        private int selectedGame_ID;
        private int[] available_IDS;
        private bool disposed = false;

        public Setup(bool firstrun = true)
        {
          
            first_time_run = firstrun;

            App.CancelClose = true;

            InitializeComponent();

            dh = new UIComponents.DialogHostContent(RootDialog, true, true);

            ReturnBack.Visibility = (!first_time_run) ? Visibility.Visible : Visibility.Hidden;

            ToolTipService.SetShowOnDisabled(SteamCMDButton, true);

            if (Config.DatabaseExists)
            {
                LiteDB.BsonValue cmdLoc = Config.GetEntryByKey("cmd", Config.INFO_COLLECTION);

                steamcmd_location = (!(cmdLoc is null)) ? cmdLoc.AsString : string.Empty;

                SteamCMDButton.IsEnabled = string.IsNullOrEmpty(steamcmd_location);

                if (!SteamCMDButton.IsEnabled)
                {
                    SteamCMDButton.ToolTip = new ToolTip { Content = $"Already set to: {steamcmd_location}", IsOpen = true };
                    Card1.IsEnabled = true;
                }

                ServerFolderButton.IsEnabled = !SteamCMDButton.IsEnabled;

                cmdLoc = null;
            }

            this.GameDropDown.ItemsSource = GetSupportedGames();
        }

        private string[] GetSupportedGames()
        {
            string file;

            string key_id = "id";
            string key_game = "game";
            string key_svr = "server";

            JObject objectA;
            JToken[] objects;

            string[] output = null;

            try
            {
                file = System.Text.Encoding.Default.GetString(SteamCMDLauncher.Properties.Resources.dedicated_server_list);
                
                objectA = JObject.Parse(file);

                objects = objectA[key_svr].Children().ToArray();

                int size = objects.Length;

                available_IDS = new int[size];
                output = new string[size];

                for (int i = 0; i < size; i++)
                {
                    available_IDS[i] = objects[i].Value<int>(key_id);
                    output[i] = objects[i].Value<string>(key_game);
                }
            }
            finally
            {
                file = null;
                objectA = null;
                objects = null;

                key_game = null;
                key_svr = null;
                key_svr = null;
            }

            return output;
        }

        private string Show_AG_Dialog(string[] games)
        {
            // Holds the current game it may have found
            string current_game = string.Empty;
            
            string game_id = string.Empty;
              
            // Holds the current found number (iterator)
            int count = 1;
           
            // Holds the max found games (cached)
            int max = games.Length;

            // Loop through each game that was found
            foreach (string game in games)
            {
                // Get the game name based on the 'appid' given
                current_game = Config.GetGameByAppId(game);

                // Change the dialog to reflect if that was the game that was found
                dh.YesNoDialog($"Found Game {count++} of {max}", $"Is the game your adding '{current_game}'?");

                // Keep going if the result is pending (-1) or is false (0) - Do this until one is true (if)
                if (dh.GetResult() == 1) { game_id = game; break; }

            }
         
            current_game = null;

            return game_id;
        }

        private void Destory()
        {
            if(!disposed)
            {
                selectedGame = null;
                steamcmd_location = null;
            
                folder_location = null;
            
                available_IDS = null;

                this.GameDropDown.ItemsSource = null;
                this.GameDropDown = null;

                SteamCMDButton.ToolTip = null;

                if (!dh.Destoryed)
                {
                    dh.Destory();
                    dh = null;
                }

                disposed = true;
            }
        }

        #region Events
        private void GameDropDown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(!Card2.IsEnabled)
                Card2.IsEnabled = true;
            
            // Assign the new ID and name of the current game to download for
            selectedGame = GameDropDown.SelectedValue.ToString();
            selectedGame_ID = available_IDS[GameDropDown.SelectedIndex];

            Config.Log($"[SETUP] User selected Game: {selectedGame} ({selectedGame_ID})");
        }

        #region Button Events
        // "SteamCMD Location"
        private void SteamCMD_Click(object sender, RoutedEventArgs e)
        {        
            steamcmd_location = Config.GetFolder("steamcmd.exe", new Action(() =>
            {
                dh.OKDialog("The given path doesn't contain the 'steamcmd.exe' to install the game files! Try again.");
                this.Hide();
            }));
            
            this.Show();

            if (steamcmd_location.Length > 0)
            {
                Config.Log($"[SETUP] CMD setup is allocated in \"{steamcmd_location}\"");
                Config.AddEntry_BJSON("cmd", steamcmd_location, Config.INFO_COLLECTION);

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
                Config.Log($"[SETUP] Install location was set to: \"{folder_location}\"");

                if (!Card3.IsEnabled)
                    Card3.IsEnabled = true;
            }
        }

        private void InstallServer_Click(object sender, RoutedEventArgs e)
        {

            Config.Log("[SETUP] Starting server install process");

            // Need to turn of wait command
            dh.IsWaiting(false);

            dh.GameInstallDialog(selectedGame);

            dh.ChangePropertyText("GameInstallStatus", "Pre-running 'steamexe.exe' - waiting for result");

            Config.Log("[SETUP] Install Process 1/4: Setting up SteamCMD Object");
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

            dh.ShowDialog();

            Task _ = Task.Run(async () =>
            {
                Config.Log("[SETUP] Install Process 2/4: Running SteamCMD itself for any updates to install");
                cmd.PreRun();

                await this.Dispatcher.Invoke(async () =>
                {
                    dh.ChangePropertyText("GameInstallStatus", "Installing... Don't close this window");

                    Config.Log("[SETUP] Install Process 3/4: Installing the game");

                    await Task.Delay(3000);

                    cmd.InstallGame(selectedGame_ID, folder_location);

                    dh.ChangePropertyText("GameInstallStatus", "Installed! Return back to main page.");

                    Config.Log("[SETUP] Install Process 4/4: Game has been installed, taking user to home page");

                    await Task.Delay(100);
                    
                    Config.AddServer(selectedGame_ID, folder_location);

                    Config.Log("[SETUP] Added server information into the database");

                    await Task.Delay(2100);

                    dh.CloseDialog();

                    await Task.Delay(2100);

                    Config.Log("[SETUP] Now destroying any unmanaged objects");

                    dh.Destory();
                    
                    dh = null;
                    cmd = null;

                    Config.Log("[SETUP] Now returning back to main window");
                    App.WindowClosed(this);
                    App.WindowOpen(new main_view());
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
                string[] found_games = Config.FindGameID(folder_location);
                string appid_loc = string.Empty;

                // Creating the programmatic version of a dialog host (using MatieralDesigns)
                if(found_games.Length > 0)
                {
                    string found_game = Show_AG_Dialog(found_games);

                    if (!string.IsNullOrEmpty(found_game))
                        appid_loc = found_game;

                    found_game = null;
                }

                if(string.IsNullOrEmpty(appid_loc))
                {
                    dh.OKDialog("Sorry but the program didn't manage to find the appid based on current folders\nPlease help by stating what commands and files to run that in this program.");
                    return;
                }

                int result;
                if(!Int32.TryParse(appid_loc, out result))
                {
                    dh.OKDialog($"Unable to support (from parsing) from id '{appid_loc}' - some features may be not implemented or available");
                    return;
                }

                if(!available_IDS.Any(x => x == result))
                {
                    dh.OKDialog($"Unable to support from id '{appid_loc}' - some features may be not implemented or available");
                    return;
                }

                Config.Log($"[SETUP] Adding server with ID {appid_loc} to folder: {folder_location}");
                Config.AddServer(result, folder_location);

                dh.OKDialog("Server has been added - Returning to home screen");

                Config.Log("[SETUP] Now destroying any unmanaged objects");
                Destory();

                found_games = null;
                appid_loc = null;

                Config.Log("[SETUP] Returning back to home page");
                App.WindowClosed(this);
                App.WindowOpen(new main_view());
            }
        }
        
        // Return back to home screen (if not first time/setup)
        private void ReturnBack_Click(object sender, RoutedEventArgs e)
        {
            Config.Log("[SETUP] Now destroying any unmanaged objects");
            Destory();

            Config.Log("[SETUP] Now returning back to main window");
            App.WindowClosed(this);
            App.WindowOpen(new main_view());
        }
        #endregion

        #endregion

    }
}
