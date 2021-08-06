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
        UIComponents.DialogHostContent HostDialog;

        public main_view()
        {
            servers = Config.GetServers();

            InitializeComponent();

            HostDialog = new UIComponents.DialogHostContent(RootDialog, true, true);

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
            string folder_location = string.Empty;

            if (!System.IO.Directory.Exists(location))
            {
                HostDialog.OKDialog("Please select the new location of the server as the last one wasn't found or exists");

                folder_location = Config.GetFolder(string.Empty, string.Empty);
                if (folder_location.Length > 0)
                {
                    Config.ChangeServerFolder(id, location, folder_location);
                    //Config.AddEntry_BJSON("svr", folder_location, Config.INFO_COLLECTION);
                    Config.Log(folder_location);

                    HostDialog.OKDialog("A restart is required to make full effect - Refreshing will not solve this.");
                } else { return; }
            }
            else
            System.Diagnostics.Process.Start("explorer.exe", location); 
        }

        private void loadServerView(string id, string al)
        {
            if(!(servers is null))
            {
                if(!System.IO.Directory.Exists(servers[id][1]))
                {
                    HostDialog.OKDialog($"That server location ({servers[id][1]}) doesn't exist anymore!\nCorrect it but stating the new location from 'View Folder' button");
                    return;
                }
            } else
            {
                HostDialog.OKDialog("Internal Problem - Not cached servers, fault with server dictionary");
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

            // Textblock which shows if no servers were found
            TextBlock text = new TextBlock()
            {
                Text = "No Servers Were Found - Add Some!",
                FontWeight = FontWeights.DemiBold, FontSize = 20, Foreground = new SolidColorBrush(Colors.White)
            };

            ServerStack.VerticalAlignment = (servers?.Count == 0) ? VerticalAlignment.Top : VerticalAlignment.Center;

            // Loop over each record stored
            if (servers?.Count == 0)
            {
                ServerStack.Children.Add(text);

                // Dereference the object as we don't need it anymore
                Card = null; text = null;
                GC.WaitForFullGCComplete(); GC.Collect();

                return;
            }

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
