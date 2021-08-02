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

namespace SteamCMDLauncher
{
    /// <summary>
    /// Interaction logic for ServerView.xaml
    /// </summary>
    public partial class ServerView : Window
    {
        private Timer waitTime;
        private string id, alias;
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

        public ServerView(string id, string alias)
        {
            this.id = id;
            this.alias = alias;

            waitTime = new Timer(1000);
            waitTime.Elapsed += TimerElapsed;
            waitTime.AutoReset = true;

            InitializeComponent();
            this.DataContext = this;
        }

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
                    Console.Beep();
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
