using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
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
    /// Interaction logic for main_view.xaml
    /// </summary>
    public partial class main_view : Window
    {
        Dictionary<string, string[]> servers;

        public main_view()
        {
            servers = Config.GetServers();
            InitializeComponent();

            UpdateRefreshButton();

            PopulateCards();
        }

        private void UpdateRefreshButton()
        {
            MaterialDesignThemes.Wpf.PackIcon refreshButtonIcon = (MaterialDesignThemes.Wpf.PackIcon)RefreshServers.Content;

            refreshButtonIcon.Kind = (servers.Count > 0) ? 
                MaterialDesignThemes.Wpf.PackIconKind.Restart :
                MaterialDesignThemes.Wpf.PackIconKind.RestartOff;

            RefreshServers.IsEnabled = servers.Count > 0;
        }

        private void loadServerFolder(string id, string location)
        {
            if (!System.IO.Directory.Exists(location))
            {
                //TODO: Show dialog and force user to state new folder location


                return;
            }

            System.Diagnostics.Process.Start("explorer.exe", location);
        }

        private void loadServerView(string id, string al)
        {
            if(!(servers is null))
            {
                if(!System.IO.Directory.Exists(servers[id][1]))
                {
                    MessageBox.Show($"That server location ({servers[id][1]}) doesn't exist anymore!\nCorrect it but stating the new location from 'View Folder' button");
                    return;
                }
            } else
            {
                MessageBox.Show("Internal Problem - Not cached servers, fault with server dictionary");
                return;
            }

            servers = null;
            GC.Collect();

            var server_window = new ServerView(id, al);
            server_window.Show();
            this.Close();
        }

        // Better over-head heap: -0.39KB (+824 objects)
        private void PopulateCards()
        {
            // Create a card instance
            var Card = new UIComponents.ServerCard();

            Card.View_Server += loadServerView;
            Card.View_Folder += loadServerFolder;

            // Check if any updates are needed since last update
            if(Config.Require_Get_Server)
                servers = Config.GetServers();

            // Loop over each record stored
            if (servers is null) return;

            foreach (var item in servers)
            {
                ServerStack.Children.Add(
                    Card.CreateCard(
                        Config.GetGameByAppId(item.Value[0]), // The games ID (740, 90 etc)
                        item.Value[2], // The alias name if set by the user
                        item.Value[1], // The folder of where the file is located
                        item.Key // The _id from the database (unique id)
                    )
                );
            }

            // Dereference the object as we don't need it anymore
            Card = null;

            GC.WaitForFullGCComplete(); GC.Collect();
        }

        private void refreshCards()
        {
            int len = ServerStack.Children.Count;
            for (int i = 0; i < len; i++)
            {
                ((MaterialDesignThemes.Wpf.Card)(ServerStack.Children[i])).UpdateCard();
            }
        }

        [Obsolete(message: "A newer better heap version is in use")]
        // Worse over-head heap: +0.39KB (+831 objects)
        private void PopulateCards_old()
        {
            int serverCount = 1;

            foreach (var item in servers)
            {
                MaterialDesignThemes.Wpf.Card card = new MaterialDesignThemes.Wpf.Card();
                card.Width = 300;
                card.Height = 200;
                card.Padding = new Thickness(2,10,2,5);
                card.Margin = new Thickness(10, 0, 10, 0);

                StackPanel panel = new StackPanel();

                // Game Title
                Button viewButton = new Button();
                viewButton.Content = "View Folder";
                viewButton.Margin = new Thickness(75, 0, 75, 10);

                Button viewServer = new Button();
                viewServer.Content = "View";
                viewServer.Margin = new Thickness(100, 0, 100, 0);

                panel.Children.Add(new TextBlock { 
                    Text = Config.GetGameByAppId(item.Value[0]),
                    FontSize=16,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(4, 0, 4, 10)
                });

                viewButton.Click += (_, e) =>
                {
                    Process.Start("explorer.exe", item.Value[1]);
                };

                panel.Children.Add(new Separator { Margin = new Thickness(0,0,0,20) });

                panel.Children.Add(new TextBlock
                {
                    Text = (String.IsNullOrEmpty(item.Value[2])) ? $"Server-{serverCount++}" : item.Value[2],
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(4,0,4,40)
                });

                panel.Children.Add(viewButton);
                panel.Children.Add(viewServer);

                card.Content = panel;
                
                ServerStack.Children.Add(card);

            }

            GC.WaitForFullGCComplete();
            GC.Collect();
        }

        private void NewServer_Click(object sender, RoutedEventArgs e)
        {
            var setup = new Setup(false);
            this.Close();
            setup.Show();
        }

        bool shown = true;
        private void Window_StateChanged(object sender, EventArgs e)
        {
            shown = !shown;
            if (shown) 
            { 
                refreshCards(); 
                UpdateRefreshButton(); 
            }
        }

        private void RefreshServers_Click(object sender, RoutedEventArgs e)
        {
            refreshCards();
        }
    }
}
