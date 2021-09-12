using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;

namespace SteamCMDLauncher
{
    /// <summary>
    /// Interaction logic for ServerView.xaml
    /// </summary>
    public partial class ServerView : Window
    {
        #region Attributes
        private System.Timers.Timer waitTime;
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
        public string NotReadyReason { get; private set; }

        private bool prevent_update = false;
        private bool toggleServerState = false;
        private bool closeStateMade = false;
        public bool IsReady { get; private set; }

        private DateTime timeStart;

        private int server_run_id;

        private Dictionary<string, string> config_files;
        #endregion

        private bool disposed = false;

        public ServerView(string id, string alias, string folder, string app_id = "")
        {
            this.IsReady = false;
            this.id = id;
            this.alias = alias;
            this.appid = (string.IsNullOrEmpty(app_id)) ? string.Empty : app_id;
            this.folder = folder;

            waitTime = new System.Timers.Timer(1000);
            waitTime.Elapsed += TimerElapsed;
            waitTime.AutoReset = true;

            gsm = new Component.GameSettingManager(appid, folder);

            if (!gsm.ResourceFolderFound)
            {
                NotReadyReason = "Failed to find the resource folder - please ensure your not running outside the application folder!"; 
                return;
            }

            if (!gsm.Supported)
            {
                NotReadyReason = "Game not supported yet\nPlease create the json files or wait till the game gets supported!"; 
                return;
            }

            if (!gsm.LanguageSupported)
            {
                NotReadyReason = "The current config file for this game is in ENGLISH for now: Please contribute to translating it!"; 
                return;
            }

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
        private void TimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            // Work around for: 'The calling thread must be STA' error
            // And 'because a different thread owns it' error
            Application.Current.Dispatcher.Invoke((Action)delegate {  
                Keyboard.ClearFocus(); 
                waitTime.Stop();

                if (!prevent_update && !alias.Same(ServerAlias.Text))
                {
                    alias = ServerAlias.Text.Trim();
                    Config.ChangeServerAlias(id, ServerAlias.Text.Trim());
                }
            });
            waitTime.Interval = 1000;
        }

        private void ServerAlias_KeyUp(object sender, KeyEventArgs e)
        {
            waitTime.Stop();

            waitTime.Interval += 250;

            ServerAlias.GetBindingExpression(TextBox.TextProperty).UpdateSource();

            if (ServerAlias.GetBindingExpression(TextBox.TextProperty).HasError)
            {
                waitTime.Stop();
                waitTime.Interval = 1000;
                prevent_update = true;
            } 
            else
            {
                if (prevent_update) prevent_update = false;
            }
            
            waitTime.Start();
        }

        private void DeleteServer_Click(object sender, RoutedEventArgs e)
        {
            dh.YesNoDialog($"Delete {this.alias}", "Are you sure you want to do this? You can add this later on if need be.\nIt will only remove the instance - not the server folder.", new Action(() =>
            {
                bool result = Config.RemoveServer(id);

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
        private void DoVerification(bool update = false)
        {
            Config.AddLog(id, update ? Config.LogType.ServerUpdate : Config.LogType.ServerValidate, "Operation Started");
            dh.ForceDialog((update) ?
            "Server is now updating.\nThis may take a long while..."
            : "Server is now validating and updating.\nThis may take a long while...", new Task(() =>
            {
                LiteDB.BsonValue cmdLoc = Config.GetEntryByKey("cmd", Config.INFO_COLLECTION);

                Component.SteamCMD cmd = new Component.SteamCMD(cmdLoc);

                cmd.Verify(Convert.ToInt32(appid), folder, update);

                this.Dispatcher.Invoke(() =>
                {
                    dh.CloseDialog();
                });
            }));
            Config.AddLog(id, update ? Config.LogType.ServerUpdate : Config.LogType.ServerValidate, "Operation Finished");
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

            string file_location = first_save ? Path.Combine(cfg_path, $"{name}.cfg") : last_save_location;
            
            if(string.IsNullOrEmpty(last_save_location) || !file_location.Same(last_save_location))
                last_save_location = name;

            File.WriteAllLines(file_location, file);

            dh.YesNoDialog("Reveal File", "The config file was successful!\nWould you like to access it directly right now?", new Action(() =>
            {
                System.Diagnostics.Process.Start("explorer.exe", file_location);
            }));

            file_location = null;
            cfg_path = null;
            name = null;
            file = null;

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
            await Task.Run(async () =>
            {
                await this.Dispatcher.Invoke(async() =>
                {
                    dh.IsWaiting(false);
                    dh.ShowBufferingDialog();
                
                    await Task.Delay(1000);

                    dh.IsWaiting(false);
                    dh.ShowBufferingDialog();
                    gsm.SetConfigFiles(File.ReadAllLines(file),
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
            string file = Config.GetFile(".cfg");
            
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
            waitTime.Elapsed -= TimerElapsed;
            waitTime.Dispose();

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

            // Perform forced GC collection to prevent memory leaks
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Load the main window again           
            App.CancelClose = true;
            App.WindowClosed(this);
            App.WindowOpen(new main_view());
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ReturnToHomePage();
            e.Cancel = true;
        }

        private void ReturnBack_Click(object sender, RoutedEventArgs e)
        {
            ReturnToHomePage();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Config.Log("[SV] Window has been fully loaded");

            if (!gsm.ConfigOffical)
            {
                Config.Log("[SV] Showing unofficial config file has been loaded");

                await Task.Run(() =>
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        dh.OKDialog("SteamCMDLauncher has identified an unofficial config has been loaded.\nThis could be either the game config json or the language config.\nSteamCMDLauncher is not responable for any damages with untrusted configurations. Please be careful!");
                    });
                });
            }
        }
    }
}