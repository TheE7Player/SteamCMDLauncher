using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using System.IO;

namespace SteamCMDLauncher
{
    /// <summary>
    /// Interaction logic for ServerView.xaml
    /// </summary>
    public partial class ServerView : Window
    {
        private System.Timers.Timer waitTime;
        private string id, alias, appid, folder;
        private bool prevent_update = false;
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

        private UIComponents.DialogHostContent dh;
        private Component.GameSettingManager gsm;
        private bool toggleServerState = false;

        private DateTime timeStart;

        public bool IsReady { get; private set; }
        public string NotReadyReason { get; private set; }

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
                NotReadyReason = "Game not supported yet"; 
                return; 
            }

            if (!gsm.LanguageSupported)
            {
                NotReadyReason = "The current config file for this game is in ENGLISH for now: Please contribute to translating it!"; 
                return; 
            }

            InitializeComponent();

            // Data context is used for binding, do not remove!
            this.DataContext = this;

            ServerGroupBox.Content = gsm.GetControls();
            
            Component.EventHooks.View_Dialog += OnHint;

            this.dh = new UIComponents.DialogHostContent(RootDialog, true, true);

            IsReady = true;
        }

        private void OnHint(string hint) => dh.OKDialog(hint);
        
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
                    dh.OKDialog("Error with deleting the server from collection and alias - If needed, clear the cache fully on next startup.\nHold 'R_CTRL' until 3 beeps are heard in next bootup.");
                    return;
                }

                dh.OKDialog("Server memory for this server location and alias is now forgotten.\nReturning home.");
                
                // Force the click of the back button with this return logic already in it
                ReturnBack.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }));
        }

        private void DoVerification(bool update = false)
        {
            Config.AddLog(id, update ? Config.LogType.ServerUpdate : Config.LogType.ServerValidate, "Operation Started");
            dh.ForceDialog((update) ?
            "Server is now updating.\nThis may take a long while..."
            : "Server is now validating and updating.\nThis may take a long while...", new Task(() =>
            {
                var cmdLoc = Config.GetEntryByKey("cmd", Config.INFO_COLLECTION);

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
            //TODO: Make saving easier if pre-loaded

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

        private void LoadConfig_Click(object sender, RoutedEventArgs e)
        {
            // Load config file
            string file = Config.GetFile(".cfg");
            
            if (string.IsNullOrEmpty(file)) return;

            last_save_location = file;

            Config.Log("[CFG] Loading settings from config file");

            var LoadTask = Task.Run(async () =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    dh.IsWaiting(false);
                    dh.ShowBufferingDialog();
                });
                
                await Task.Delay(1000);

                this.Dispatcher.Invoke(() =>
                {
                    dh.IsWaiting(false);
                    dh.ShowBufferingDialog();      
                    gsm.SetConfigFiles(File.ReadAllLines(file), new Action(() => {               
                        dh.CloseDialog();
                        dh.IsWaiting(true);
                    }));
                });
            });
        }

        private void ToggleRunButton()
        {
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

        private void ToggleServer_Click(object sender, RoutedEventArgs e)
        {
            // Start/Stop Server button
            ToggleRunButton();

            if (toggleServerState)
            {
                //Check if any fields that are required are filled
                string[] any_required_empty = gsm.RequiredFields();

                // If any fields that are required aren't filled, so an error stating which ones
                if(any_required_empty.Length > 0)
                {
                    StringBuilder sb = new StringBuilder();

                    sb.AppendLine("The following faults were caused by fields that require input:\n");
                    
                    foreach (var err in any_required_empty)
                    {
                        sb.AppendLine(err);
                    }
                    
                    dh.OKDialog(sb.ToString());
                    
                    sb.Clear(); 
                    
                    sb = null;
                    
                    ToggleRunButton();

                    return;
                }

                var cmd = new Component.SteamCMD(gsm.GetExePath, false);

                cmd.AddArgument(gsm.GetRunArgs(), gsm.GetPreArg);

                timeStart = DateTime.Now;

                Config.Log($"Running Server with set Args");

                tb_Status.Text = "Server Running";

                cmd.Run();

                // Pre-invoke the button, to update the UI state
                ToggleServer.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            } 
            else
            {
                // Get the total duration of the server being alive
                TimeSpan TotalTime = DateTime.Now - timeStart;

                tb_Status.Text = $"Server Halted\n{Math.Round(TotalTime.TotalMinutes, 2)} minutes";
            }

        }

        private void ReturnToHomePage()
        {
            id = null;
            alias = null;
            appid = null;
            folder = null;
            last_save_location = null;
            current_alias = null;
            
            waitTime.Elapsed -= TimerElapsed;
            waitTime.Dispose();
            gsm.Destory();
            dh.Destory();
           
            dh = null;
            gsm = null;
            waitTime = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Load the main window again
            main_view mv = new main_view();
            mv.Closed += App.Window_Closed;
            mv.Show();

            App.CancelClose = false;
            //this.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ReturnToHomePage();
        }

        private void ReturnBack_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}