using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;

namespace SteamCMDLauncher
{
    /// <summary>
    /// Interaction logic for main_view.xaml
    /// </summary>
    public partial class main_view : Window
    {
        Component.Struct.ServerCardInfo[] servers;
        UIComponents.DialogHostContent HostDialog;

        bool initEvents = false;

        public main_view()
        {
            // App closing after select new server fix
            if (App.CancelClose)
                App.CancelClose = false;

            Config.Log("Loaded Main Window");

            Config.Log("Getting Servers");
            servers = Config.GetServersNew();
       
            Config.Log("Initializing UI Components");
            InitializeComponent();

            AppVersion.Text = App.Version;

            Config.Log("Setting up dialog host");
            HostDialog = new UIComponents.DialogHostContent(RootDialog, true, true);

            UpdateRefreshButton();

            Config.Log("Populating Cards");
            PopulateCards();
        }

        private Component.Struct.ServerCardInfo GetServerByID(string id)
        {
            return servers.FirstOrDefault(x => x.Unique_ID == id);
        }

        private void UpdateRefreshButton()
        {
            MaterialDesignThemes.Wpf.PackIcon refreshButtonIcon = (MaterialDesignThemes.Wpf.PackIcon)RefreshServers.Content;

            refreshButtonIcon.Kind = (servers.Length > 0) ?
                MaterialDesignThemes.Wpf.PackIconKind.Restart :
                MaterialDesignThemes.Wpf.PackIconKind.RestartOff;

            RefreshServers.IsEnabled = servers.Length > 0;

            refreshButtonIcon = null;
        }

        private void LoadServerFolder(string id, string location)
        {
            string folder_location = string.Empty;

            if (!System.IO.Directory.Exists(location))
            {
                HostDialog.OKDialog("Please select the new location of the server as the last one wasn't found or exists");

                folder_location = Config.GetFolder(string.Empty, string.Empty);
                if (folder_location.Length > 0)
                {
                    Config.ChangeServerFolder(id, location, folder_location);

                    Config.Log(folder_location);

                    HostDialog.OKDialog("A restart is required to make full effect - Refreshing will not solve this.");
                } else { return; }
            }
            else
            System.Diagnostics.Process.Start("explorer.exe", location); 
        }

        private void LoadServerView(string id)
        {

            if (servers is null)
            {
                HostDialog.OKDialog("Internal Problem - Not cached servers, fault with server dictionary");
                return;
            }

            Component.Struct.ServerCardInfo current_server = GetServerByID(id);
            
            if(current_server.IsEmpty)
            {
                HostDialog.OKDialog($"Problems attempting to find server id: {id}.\nThis is either a code fault or a database fault.");
                return;
            }

            if (!System.IO.Directory.Exists(current_server.Folder))
            {
                HostDialog.OKDialog($"That server location ({current_server.Folder}) doesn't exist anymore!\nCorrect it but stating the new location from 'View Folder' button");
                return;
            }

            ServerView server_window = new ServerView(id, current_server.Alias, current_server.Folder, current_server.GameID);

            if (server_window.IsReady)
            { 
                servers = null;
                App.CancelClose = true;

                ServerStack = null;

                HostDialog.Destory();
                HostDialog = null;
                Component.EventHooks.UnhookServerCardEvents();

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                server_window.Show();
                this.Close();
            }
            else
            {
                HostDialog.OKDialog(server_window.NotReadyReason);
            }
        }

        private void PopulateCards()
        {
            // Create a card instance
            UIComponents.ServerCard Card = new UIComponents.ServerCard();

            // Check if any updates are needed since last update
            if(Config.Require_Get_Server)
                servers = Config.GetServersNew();

            Config.Log("Hooking Button Events");

            if(!initEvents)
            {
                Component.EventHooks.View_Server += LoadServerView;
                Component.EventHooks.View_Folder += LoadServerFolder;
                initEvents = true;
            }

            // Textblock which shows if no servers were found
            TextBlock text = new TextBlock()
            {
                Text = "No Servers Were Found - Add Some!",
                FontWeight = FontWeights.DemiBold, FontSize = 20, Foreground = new SolidColorBrush(Colors.White)
            };

            ServerStack.VerticalAlignment = (servers.Length == 0) ? VerticalAlignment.Top : VerticalAlignment.Center;

            // Loop over each record stored
            if (servers.Length == 0)
            {
                ServerStack.Children.Add(text);

                // Dereference the object as we don't need it anymore
                Card = null; text = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                return;
            }

            foreach (Component.Struct.ServerCardInfo svr in servers)
            {
                ServerStack.Children.Add(Card.CreateCard(Config.GetGameByAppId(svr.GameID), svr.Alias, svr.Folder, svr.Unique_ID));
            }

            // Dereference the object as we don't need it anymore
            Card = null;
            text = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private void RefreshCards()
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
                RefreshCards(); 
                UpdateRefreshButton(); 
            }
        }

        private void RefreshServers_Click(object sender, RoutedEventArgs e)
        {
            RefreshCards();
        }
    }
}
