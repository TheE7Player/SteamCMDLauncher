using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Threading;
using System.Threading.Tasks;

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

        public ServerView(string id, string alias, string folder, string app_id = "")
        {
            this.id = id;
            this.alias = alias;
            this.appid = (string.IsNullOrEmpty(app_id)) ? string.Empty : app_id;
            this.folder = folder;

            waitTime = new System.Timers.Timer(1000);
            waitTime.Elapsed += TimerElapsed;
            waitTime.AutoReset = true;

            gsm = new Component.GameSettingManager(appid, folder);

            if(!gsm.ResourceFolderFound)
                MessageBox.Show("Failed to find the resource folder - please ensure your not running outside the application folder!");

            if (!gsm.Supported)
                MessageBox.Show("Game not supported yet");

            if (!gsm.LanguageSupported)
                MessageBox.Show("The current config file for this game is in ENGLISH for now: Please contribrute to translating it!");

            InitializeComponent();

            // Data context is used for binding, do not remove!
            this.DataContext = this;

            ServerGroupBox.Content = gsm.GetControls();
            
            gsm.View_Dialog += OnHint;

            this.dh = new UIComponents.DialogHostContent(RootDialog, true, true);
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
            } else
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
            // Save config button
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
                var any_required_empty = gsm.RequiredFields();

                if(any_required_empty.Length > 0)
                {
                    var sb = new StringBuilder();

                    sb.AppendLine("The following faults were caused by fields that require input:\n");
                    
                    foreach (var err in any_required_empty)
                    {
                        sb.AppendLine(err);
                    }
                    
                    dh.OKDialog(sb.ToString());
                    
                    sb.Clear(); sb = null;
                    
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

        private void ReturnBack_Click(object sender, RoutedEventArgs e)
        {
            this.id = null;
            this.alias = null;

            // Load the main window again
            main_view mv = new main_view();
            mv.Show();

            this.Close();
        }
    }
}
