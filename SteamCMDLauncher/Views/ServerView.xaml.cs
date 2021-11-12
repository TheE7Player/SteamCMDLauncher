using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SteamCMDLauncher
{
    /// <summary>
    /// Interaction logic for ServerView.xaml
    /// </summary>
    public partial class ServerView : Window
    {
        public enum FaultReason
        {
            NoResourceFolder,
            GameNotSupported,
            CultureNotSupported,
            BrokenEmbedFile
        }

        #region Attributes
        private System.Windows.Threading.DispatcherTimer waitTime;
        private UIComponents.DialogHostContent dh;
        private Component.GameSettingManager gsm;

        private string id, alias, appid, folder;
        private string last_save_location;

        private string current_alias = string.Empty;
        public string Alias
        {
            get
            {
                if (current_alias.Length == 0) current_alias = alias;
                return current_alias;
            }
            set
            {
                current_alias = value;
            }
        }
        public FaultReason NotReadyReason { get; private set; }

        private bool prevent_update = false;
        private bool toggleServerState = false;
        private bool closeStateMade = false;
        public bool IsReady { get; private set; }

        private DateTime timeStart;

        private int server_run_id;

        private Dictionary<string, string> config_files;
        #endregion

        private bool disposed = false;

        public string GetFaultReason() => NotReadyReason switch
        {
            FaultReason.NoResourceFolder => "Failed to find the resource folder - please ensure your not running outside the application folder!",
            FaultReason.GameNotSupported => "Game not supported yet\nPlease create the json files or wait till the game gets supported!",
            FaultReason.CultureNotSupported => "The current config file for this game is in ENGLISH for now: Please contribute to translating it!",
            FaultReason.BrokenEmbedFile => "A fault from the Embedded Resource has occurred.\nPlease look at the release log file to see what resource failed.",
            _ => "Unknown"
        };

        public ServerView(string id, string alias, string folder, string app_id = "")
        {
            this.IsReady = false;
            this.id = id;
            this.alias = alias;
            this.appid = app_id ?? string.Empty;
            this.folder = folder;

            waitTime = new System.Windows.Threading.DispatcherTimer();
            waitTime.Tick += TimerElapsed;
            waitTime.Interval = TimeSpan.FromSeconds(3);

            gsm = new Component.GameSettingManager(appid, folder);

            if (!gsm.ResourceFolderFound) { NotReadyReason = FaultReason.NoResourceFolder; return; }
            if (!gsm.Supported) { NotReadyReason = FaultReason.GameNotSupported; return; }
            if (!gsm.LanguageSupported) { NotReadyReason = FaultReason.CultureNotSupported; return; }
            if (gsm.BrokenEmbededResource) { NotReadyReason = FaultReason.BrokenEmbedFile; return; }

            this.Loaded += Window_Loaded;

            InitializeComponent();

            // Data context is used for binding, do not remove!
            this.DataContext = this;

            ServerGroupBox.Content = gsm.GetControls();
            
            Component.EventHooks.View_Dialog += OnHint;

            this.dh = new UIComponents.DialogHostContent(RootDialog, true, true);

            IsReady = true;
        }

        private void OnHint(string hint) => dh.OKDialog(hint);

        #region Server Name/ Delete Server
        private async void TimerElapsed(object sender, EventArgs e)
        {
            Keyboard.ClearFocus();
            waitTime.Stop();

            if (!prevent_update && !alias.Same(ServerAlias.Text))
            {
                alias = ServerAlias.Text.Trim();
                Config cfg = new Config(); 
                if (cfg.ChangeServerAlias(id, ServerAlias.Text.Trim()))
                {
                    await Task.Delay(4000);
                    LogButton.IsEnabled = true;
                }
                cfg = null;
            }
        }

        private void ServerAlias_KeyUp(object sender, KeyEventArgs e)
        {
            waitTime.Stop();

            //waitTime.Interval += 250;

            ServerAlias.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            
            LogButton.IsEnabled = false;
            
            if (ServerAlias.GetBindingExpression(TextBox.TextProperty).HasError)
            {
                waitTime.Stop();
                //waitTime.Interval = 1000;
                prevent_update = true;
                LogButton.IsEnabled = true;
            } 
            else
            {
                if (prevent_update) prevent_update = false;
            }
            
            waitTime.Start();

            sender = null; e = null;
        }

        private void DeleteServer_Click(object sender, RoutedEventArgs e)
        {
            dh.YesNoDialog($"Delete {this.alias}", "Are you sure you want to do this? You can add this later on if need be.\nIt will only remove the instance - not the server folder.", new Action(() =>
            {
                Config cfg = new Config(); 
                bool result = cfg.RemoveServer(id);
                cfg = null;
                if(!result)
                {
                    dh.OKDialog("Error with deleting the server from collection and alias - If needed, clear the cache fully on next startup.\nHold 'R_CTRL' until 1 beep is heard in next boot up.");
                    return;
                }

                dh.OKDialog("Server memory for this server location and alias is now forgotten.\nReturning home.");
                
                // Force the click of the back button with this return logic already in it
                ReturnBack.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }));
        }
        #endregion

        #region Server Operations
        private async void DoVerification(bool update = false)
        {
            if(!Component.Win32API.IsConnectedToInternet())
            {
                dh.OKDialog("Unable to perform verification due to no Internet access available.");
                return;
            }

            Config cfg = new Config();

            cfg.AddLog(id, update ? Config.LogType.ServerUpdate : Config.LogType.ServerValidate, "Operation Started");
            dh.ForceDialog((update) ?
            "Server is now updating.\nThis may take a long while..."
            : "Server is now validating and updating.\nThis may take a long while...", new Task(() =>
            {
                LiteDB.BsonValue cmdLoc = cfg.GetEntryByKey("cmd", Config.INFO_COLLECTION);

                Component.SteamCMD cmd = new Component.SteamCMD(cmdLoc);

                cmd.Verify(Convert.ToInt32(appid), folder, update);

                this.Dispatcher.Invoke(() =>
                {
                    dh.CloseDialog();
                });
            }));

            cfg.AddLog(id, update ? Config.LogType.ServerUpdate : Config.LogType.ServerValidate, "Operation Finished");

            // Call this function again to set the build numbers
            await IsGameServerUpdated(folder, appid, true);

            cfg = null;
        }

        private void ValidateServer_Click(object sender, RoutedEventArgs e)
        {
            DoVerification();
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            DoVerification(true);
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            // Validate if any required files are needed fist

            //Check if any fields that are required are filled
            if (!ValidateInputs(true)) return;

            // Save config button
            string[] file = gsm.GetSafeConfig();

            bool suitable = true;
            string name = string.Empty;

            bool first_save = string.IsNullOrEmpty(last_save_location);

            if (first_save)
            {
                dh.InputDialog("Save Configuration File", "The config file will be saved near the .exe location - what shall you call it?", new Action<string>((t) =>
                {
                    // Validating if the name is suitable
                    suitable = t.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
                    name = t;
                }));

                if (!suitable)
                {
                    dh.OKDialog($"Couldn't accept '{name}' as it contains illegal characters for a file name\nTry again with a better one!");
                    return;
                }
            }

            Config.Log("[CFG] Creating config file");

            // Lets get the path sorted...
            string cfg_path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "configs");

            if (!Directory.Exists(cfg_path))
                Directory.CreateDirectory(cfg_path);

            string file_location = first_save ? Path.Combine(cfg_path, $"{name}{Component.Archive.DEFAULT_EXTENTION_SETTING}") : last_save_location;
            
            if(string.IsNullOrEmpty(last_save_location) || !file_location.Same(last_save_location))
                last_save_location = name;

            var save_config = new Component.Archive(file_location, appid, true);

            save_config.SetFileContents("config.cfg", string.Join("\r\n",file));

            save_config.SaveFile();

            save_config.Cleanup = true;
            save_config.ForceClear();
            save_config = null;

            string temp_path = Path.Combine(Component.Archive.CACHE_PATH, $"{name}.cfg");
            File.WriteAllLines(temp_path, file);

            dh.YesNoDialog("Reveal File", "The config file was successful!\nWould you like to access it directly right now?\n(Note: This is a temp file as editing the config is not possible outside this application)", new Action(() =>
            {
                System.Diagnostics.Process.Start("explorer.exe", temp_path);
            }));

            file_location = null;
            cfg_path = null;
            name = null;
            file = null;
            temp_path = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private async Task<bool> LoadConfigFile(string file)
        {
            if (!File.Exists(file)) return false;

            string short_name;

            if (config_files is null)
            { 
                config_files = new Dictionary<string, string>(5);
            }
            else
            {
                bool file_loaded = config_files.ContainsValue(file);
                bool same_loc = last_save_location.Same(file);

                if (file_loaded && same_loc)
                {
                    return false;
                }
            }

            bool force_out = false;

            var arch = new Component.Archive(file);

            if(arch.GetArchiveDetails.GameID != appid)
            {
                Config.Log("[SV] LoadConfigFile returned false as the appid didn't match");
                return false;
            }

            string[] contents = null;

            foreach (var item in arch.GetFiles())
            {
                if(item.Item1 == "config.cfg")
                {
                    contents = item.Item2.Split(Environment.NewLine);
                    break;
                }
            }

            arch.Cleanup = true;
            arch.ForceClear();
            arch = null;

            if(contents is null)
            {
                Config.Log("[SV] LoadConfigFile returned false as the config file while reading the archive in read-mode failed");
                return false;
            }

            await Task.Run(async () =>
            {
                await this.Dispatcher.Invoke(async() =>
                {
                    dh.IsWaiting(false);
                    dh.ShowBufferingDialog();
                
                    await Task.Delay(1000);

                    dh.IsWaiting(false);
                    dh.ShowBufferingDialog();
                    gsm.SetConfigFiles(contents,
                    new Action(() => {
                        dh.CloseDialog();
                        dh.IsWaiting(true);
                    }), 
                    new Action(() => { 
                        dh.CloseDialog();
                        dh.IsWaiting(true);
                        force_out = true;
                    }));
                });

                if (force_out) return;

                short_name = Path.GetFileNameWithoutExtension(file);
                
                if (!config_files.ContainsKey(short_name))
                    config_files.Add(short_name, file);

                short_name = null;
            });

            return !force_out;
        }

        private async void LoadConfig_Click(object sender, RoutedEventArgs e)
        {
            // Load config file
            string file = Config.GetFile(Component.Archive.DEFAULT_EXTENTION_SETTING);
            
            if (string.IsNullOrEmpty(file)) return;

            Config.Log("[CFG] Loading settings from config file");

            bool result = await LoadConfigFile(file);

            last_save_location = file;

            if(!result)
            {
                Config.Log($"[LoadConfig] async returned false: \"{file}\"");
                dh.OKDialog("A problem occurred. Either:\nA) The config you loaded is already been stored (Use the ComboBox top right)\nB) The config you selected isn't for the controls available or is corrupted.");
                return;
            }
            
            LoadConfigBox(file);
            
            file = null;
        }

        private void ToggleRunButton()
        {
            Config.Log("[SV] ToggleRunButton() was acknowledged");

            // Perform the toggle
            toggleServerState = !toggleServerState;

            // We cannot just set content, as this clears the icon as well - We need to get the stackpanel instance
            // [NOTE]: This acts like a ref state, don't need to reassign the data
            StackPanel buttonState = (StackPanel)ToggleServer.Content;

            // [0] PackIcon, [1] TextBlock

            // Cast the children object to 'PackIcon' and changes its kind (icon/symbol) based on the state
            ((MaterialDesignThemes.Wpf.PackIcon)buttonState.Children[0]).Kind = (toggleServerState) ? 
                MaterialDesignThemes.Wpf.PackIconKind.Stop : 
                MaterialDesignThemes.Wpf.PackIconKind.PlayArrow;

            // Cast the children object to 'TextBlock' then changes its text property based on the state given
            ((TextBlock)buttonState.Children[1]).Text = (toggleServerState) ? 
                "Stop Server" : "Start Server";
        }

        private bool ValidateInputs(bool toggleButton = false)
        {
            bool result = false;

            //Check if any fields that are required are filled
            string[] any_required_empty = gsm.RequiredFields();

            // If any fields that are required aren't filled, so an error stating which ones
            if (any_required_empty.Length > 0)
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine("The following faults were caused by fields that require input:\n");

                foreach (var err in any_required_empty)
                {
                    sb.AppendLine(err);
                }

                dh.OKDialog(sb.ToString());

                sb = null;
                any_required_empty = null;
                
                if(toggleButton) ToggleRunButton();
            } 
            else
            {
                result = true;
            }
 
            return result;
        }
        
        private async Task ServerStart()
        {
            Config.Log("[SV] ServerStart() logic started");

            ToggleRunButton();

            //Check if any fields that are required are filled
            if (!ValidateInputs(true)) return;

            Component.SteamCMD cmd = new Component.SteamCMD(gsm.GetExePath, false);

            cmd.AddArgument(gsm.GetRunArgs(), gsm.GetPreArg);

            string c_str = gsm.GetConnectCommand();

            if(!string.IsNullOrEmpty(c_str))
            {
                await Task.Run(() => {
                    this.Dispatcher.Invoke(() =>
                    {
                        dh.YesNoDialog("Save join command to clipboard", "Success, Everything is good to go!\nWould you like to copy the connect command to your clipboard? (Recommended)", new Action(() =>
                        {
                            TextCopy.ClipboardService.SetText(c_str);
                        }));
                    });
                });
            }
            else
            {
                Config.Log("[SV] JSON didn't state if there is a connection command, assuming user knows these!");
            }

            timeStart = DateTime.Now;

            Config.Log("[SV] Running Server");

            tb_Status.Text = "Server Running";
            
            server_run_id = cmd.Run(new Action(() => {
                // Pre-invoke the button, to update the UI state
                this.Dispatcher.Invoke(() =>
                {
                    ServerStop();
                });
            }));

            await App.ForceNotify("Server Running",
            "Your server is now running. Click here to access the panel again!",
            System.Windows.Forms.ToolTipIcon.Info,
            300,
            true, true, false);

            this.Visibility = Visibility.Collapsed;

            if (server_run_id == -1)
                throw new Exception("ServerView for running the server program has returned -1, something went wrong. The process may have been executed but cannot be controlled by program.");

            c_str = null;
        }

        private void ServerStop()
        {
            // Prevent double run of the program (if the user forced closes, the event calls it again - this makes this run twice instead of once!)
            if (closeStateMade) return;
            closeStateMade = true;

            Config.Log("[SV] ServerStop() logic started");

            ToggleRunButton();

            // Get the total duration of the server being alive

            if (server_run_id <= 0)
            {
                Config.Log($"[SV] server_run_id has returned {server_run_id}, that was not requested or expected!");
                dh.OKDialog($"An error has occurred, as the process id is '{server_run_id}'.\nThis was unexpected and should be reported to issues in the GitHub Page.");
                ToggleRunButton();
                return;
            }

            System.Diagnostics.Process.GetProcessById(server_run_id).Kill();

            TimeSpan TotalTime = DateTime.Now - timeStart;

            StringBuilder sb = new StringBuilder(8);

            // Deal with hours first
            sb.Append(TotalTime.Hours > 9 ? $"{TotalTime.Hours}:" : $"0{TotalTime.Hours}:");

            // Then the minutes
            sb.Append(TotalTime.Minutes > 9 ? $"{TotalTime.Minutes}:" : $"0{TotalTime.Minutes}:");

            // Finally the seconds
            sb.Append(TotalTime.Seconds > 9 ? $"{TotalTime.Seconds}" : $"0{TotalTime.Seconds}");

            tb_Status.Text = $"Server Halted: {sb}";
            
            sb = null;

            Window self = App.GetActiveWindow().Target as Window;
            SteamCMDLauncher.Component.Win32API.ForceWindowOpen(ref self);
            self = null;
        }
        
        // Start/Stop Server button
        private async void ToggleServer_Click(object sender, RoutedEventArgs e)
        {
            if (!toggleServerState)
            {
                Config.Log("[SV] ServerStart() was acknowledged");
                closeStateMade = false;
                await ServerStart();
            }
            else
            {
                Config.Log("[SV] ServerStop() was acknowledged");
                ServerStop();
            }
        }

        /// <summary>
        /// Gets an API call to check if the current local server is the latest version
        /// </summary>
        /// <param name="server_dir">The folder location of the ROOT server folder</param>
        /// <param name="app_id">the servers id number (not the parent game id!)</param>      
        /// <returns>1 if true (needs updated), 0 if false (no update) or -1 if no Internet</returns>
        private async ValueTask<int> IsGameServerUpdated(string server_dir, string app_id, bool force_check = false)
        {
            if (!Component.Win32API.IsConnectedToInternet()) { Config.Log("[SV] [!] IsGameServerUpdated: Cannot update as there was no Internet to begin with. [!]"); return -1; }

            Config cfg = new Config();

            if (!force_check) { 

                int require_update = cfg.RequireUpdate(id);

                if (require_update > -1) return require_update; 
            }

            Config.Log("[SV] IsGameServerUpdated: Creating Unmanaged Objects");
            // We first use the https://www.steamcmd.net API to get the latest fork of the current server
            
            // Create our already defined strings (we know at compile time)
            string pattern = @"\""public\"":\s+{\""buildid\"":\s+\""(\d+)\""";
            string app_manif_loc = Path.Combine(server_dir, $"steamapps\\appmanifest_{app_id}.acf");
            string match_target = "buildid";

            // Create the runtime assigned variables (as it will be determined while running, unknown at compile time)
            string response = null;
            string server_buildid = null;
            string[] local_file = null;
            string build_id_index_lc = null;
            Match webRegex = null;
            Config.Log("[SV] IsGameServerUpdated: Completed Unmanaged Objects");
        
            try
            {
                Config.Log("[SV] IsGameServerUpdated: Starting API Call");
                
                // Setup the HTTP Client
                System.Net.HttpWebRequest httpRequest = (System.Net.HttpWebRequest)System.Net.WebRequest.Create($"https://api.steamcmd.net/v1/info/{app_id}");

                // Tell the request we'd expect a JSON response back
                httpRequest.Accept = "application/json";
                System.Net.WebResponse httpResponse = null;
                StreamReader streamReader = null;
                //bool ResponceMade = false;

                try
                {
                    await httpRequest.GetResponseAsync().ContinueWith(r =>
                    {
                        if (!disposed)
                        { 
                            //ResponceMade = true;
                            httpResponse = r.Result;
                            streamReader = new StreamReader(httpResponse.GetResponseStream());
                            response = streamReader.ReadToEndAsync().Result;
                        }
                        else
                        {
                            Config.Log("[SV] IsGameServerUpdated: API Call Disturbed - Forced Called due to disposed window.");
                            httpRequest.Abort();
                        }
                    });                                         
                }
                catch (Exception ex)
                {
                    Config.Log("[SV] [!] IsGameServerUpdated: ERROR ON WEB REQUEST - LOOK BELOW FOR REASON [!] ");
                    Config.Log($"[SV] [!] IsGameServerUpdated: {ex.Message}");
                    ex = null;
                    return -1;
                }
                finally
                {
                    streamReader?.Close();                  
                    httpResponse?.Close();
                    
                    streamReader?.Dispose();
                    httpResponse?.Dispose();

                    httpResponse = null;
                    streamReader = null;
                }

                if (response == null) { Config.Log("[SV] [!] IsGameServerUpdated: 'response' was empty - this shouldn't be the case! [!] "); return -1; }

                if (disposed) { Config.Log("[SV] IsGameServerUpdated: API Call Finished and Cancelled: Form has been disposed, ignoring checks..."); return 0; }

                // Now we use regex to find the id
                Config.Log("[SV] IsGameServerUpdated: Finished API Call");
                
                webRegex = Regex.Match(response, pattern, RegexOptions.Multiline);

                if(!webRegex.Success)
                {
                    Config.Log("[SV] IsGameServerUpdated: Issue with API call - Failed to get 'buildid' from JSON Responce");
                    dh.OKDialog("An issue has occurred as the program wasn't able to identify a 'buildid' from the Server API.\nUpdate the server manually.");
                    return 0;
                }

                Config.Log("[SV] IsGameServerUpdated: Found latest server 'buildid'");
                server_buildid = webRegex.Groups[1].Value;

                // Now we check if the appmanifest.acf is available

                Config.Log("[SV] IsGameServerUpdated: Check if the server root has a manifest file");
                // Validate if such a file exists
                if (!File.Exists(app_manif_loc))
                {
                    Config.Log($"[SV] IsGameServerUpdated: Missing App Manifest File for {app_id}, located in: {server_dir}");
                    return 0;
                }

                Config.Log("[SV] IsGameServerUpdated: Manifest file was found");
                // This means it does exist - Time to validate the current build version
                local_file = File.ReadAllLines(app_manif_loc);
            
                // Get the max array length, as this will likely not change over time (cache saving)
                int app_m_idx = local_file.Length;
                bool mFound = false;
                
                Config.Log("[SV] IsGameServerUpdated: Manifest file 'buildid' find is under way");
                
                for (int i = 0; i < app_m_idx; i++)
                {
                    if (local_file[i].Length < 8) continue;

                    build_id_index_lc = local_file[i].Trim();

                    if (build_id_index_lc[1..8] == match_target)
                    {
                        // Skip until the second last quote mark (")
                        // Remove the last quote mark (")
                        // Which returns the buildid by itself
                        build_id_index_lc = build_id_index_lc[12..^1];
                        mFound = true;
                        // Stop as we got the information we need
                        break; 
                    }
                }

                if(!mFound)
                {
                    Config.Log("[SV] IsGameServerUpdated: 'buildid' was not found in the manifest file!");
                    dh.OKDialog("Couldn't find 'buildid' tag in the manifest file - an update check couldn't be performed.\nPerform your own update if needed!");
                    return 0;
                }

                Config.Log("[SV] IsGameServerUpdated: Manifest file 'buildid' find is complete");

                cfg.SetCurrentBuildVersion(id, build_id_index_lc, server_buildid);

                // Now we compare if both strings are equal (same version)
                // We're comparing the server to the local, as we cannot guarantee if the local is the same as server
                if (mFound && !string.Equals(server_buildid, build_id_index_lc))
                {
                    return 1;
                }
            }
            finally
            {
                // Dispose / Dereference any unmanaged objects
                cfg = null;

                pattern = null;
                app_manif_loc = null;
                match_target = null;

                response = null;
                server_buildid = null;
                local_file = null;
                build_id_index_lc = null;
                webRegex = null;

                local_file = null;
                app_manif_loc = null;
                server_dir = null;
                response = null;
                app_id = null;
            }

            // By default return 'false' if 'true' doesn't called
            return 0;
        }
        #endregion

        #region Config Box Related

        bool toggleColour = false;

        // Toggles between Black and White depending if the dropdown menu is option
        private System.Windows.Media.Brush ChangeForeground_ConfigBox => (toggleColour = !toggleColour) ?
            System.Windows.Media.Brushes.Black:
            System.Windows.Media.Brushes.White;
        
        private void LoadConfigBox(string file_name)
        {
            if (!configBox.IsEnabled) configBox.IsEnabled = true;

            string displayName = Path.GetFileNameWithoutExtension(file_name);

            if (!configBox.Items.Contains(displayName))
            {
                configBox.Items.Add(displayName);
            }

            configStatus.Text = "Loaded";

            // Assign the combobox index

            if (configBox.Items.Count == 1)
            {
                configBox.SelectedIndex = 0;
            }
            else
            {
                int idx = configBox.Items.IndexOf(displayName);
                configBox.SelectedIndex = idx;
            }

            displayName = null;
            file_name = null;
        }

        private async void configBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (configBox.Items.Count <= 1 ) { return; }

            string target = config_files[configBox.SelectedValue.ToString()];

            bool result = await LoadConfigFile(target);

            if (!result)
            {
                Config.Log($"[LoadConfig] async returned false: \"{target}\"");
                dh.OKDialog("That config file is missing or is already stored!\nIf loaded previously, use the ComboBox in the top right to load it in again.");
                return;
            }

            if (!last_save_location.Same(target))
                last_save_location = target;

            target = null;
            e = null;
            sender = null;
        }

        private void ToggleConfigBoxColour(object sender, EventArgs e)
        {
            configBox.Foreground = ChangeForeground_ConfigBox;
            sender = null; e = null;
        }
        #endregion

        private void ReturnToHomePage()
        {
            if (disposed) return;
            disposed = true;

            // Deference all string types
            id = null;
            alias = null;
            appid = null;
            folder = null;
            last_save_location = null;
            current_alias = null;
            config_files = null;
            // Use any method with its own deconstructors, events or disposable methods
            waitTime.Tick -= TimerElapsed;
            waitTime = null;

            configBox.DropDownClosed -= ToggleConfigBoxColour;
            configBox.DropDownOpened -= ToggleConfigBoxColour;

            gsm.Destory();
            dh.Destory();

            // Nullify the objects now
            dh = null;
            gsm = null;
            waitTime = null;

            // Dereferencing UI elements
            configBox = null;
            configStatus = null;
            ServerAlias = null;
            RootDialog = null;
            ServerGroupBox = null;
            this.DataContext = null;

            // Perform forced GC collection to prevent memory leaks
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Load the main window again           
            App.CancelClose = true;
            App.WindowClosed(this);
            App.WindowOpen(new main_view());
        }

        private void ViewLog_Click(object sender, RoutedEventArgs e)
        {
            Component.DBManager test = new Component.DBManager(Config.DatabaseLocation);

            Component.Struct.ServerLog data = test.ServerDetails(Config.LOG_COLLECTION, id);

            if (!data.Empty)
            {
                DataGrid table = new DataGrid();

                table.HorizontalAlignment = HorizontalAlignment.Stretch;
                table.HorizontalContentAlignment = HorizontalAlignment.Stretch;

                int len_max = data.Capacity;

                List<Component.Struct.ServerLogBinding> items = new List<Component.Struct.ServerLogBinding>(len_max);

                for (int i = 0; i < len_max; i++)
                {
                    items.Add(new Component.Struct.ServerLogBinding() { Date = data.utc_time[i], Type = data.types[i].ToString(), Reason = data.detail[i] });
                }

                table.MaxWidth = 800;
                table.MaxHeight = 350;
                table.ItemsSource = items;
                table.IsReadOnly = true;
                table.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
                table.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;

                items = null;

                dh.ShowComponent($"Server Log Details [{len_max}]", table);

                table = null;
            }
            else
            {
                dh.OKDialog("No logs were found for this server, chance that the log has been cleared or failed to note changes.");
            }

            test.Destory();
            data.Destory();

            test = null;
            sender = null;
            e = null;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ReturnToHomePage();
            e.Cancel = true;
            sender = null; e = null;
        }

        private void ReturnBack_Click(object sender, RoutedEventArgs e)
        {
            ReturnToHomePage();
            sender = null; e = null;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Config.Log("[SV] Window has been fully loaded");

            if (!gsm.ConfigOffical)
            {
                Config.Log("[SV] Showing unofficial config file has been loaded");
                dh.OKDialog("SteamCMDLauncher has identified an unofficial config has been loaded.\nThis could be either the game config json or the language config.\nSteamCMDLauncher is not responable for any damages with untrusted configurations. Please be careful!");
            }
       
            int needs_update_flag = await IsGameServerUpdated(folder, appid);

            if(!disposed)
            { 
                if(needs_update_flag == 1)
                {
                    dh.OKDialog("Your running in an older version of the local server files!\nPress 'Update Server' button to get the new files ('Validate Server' if the update is corrupted!)");
                }

                UpdateBar.IsIndeterminate = false;

                UpdateBarText.Text = needs_update_flag switch
                {
                    -1 => "NO INTERNET / SERVER FAULT",
                    0  => "RECENT VERSION",
                    1  => "UPDATE SERVER",
                    _  => $"ERROR: GOT {needs_update_flag}"
                };
            }
            sender = null; e = null;
        }
    }
}