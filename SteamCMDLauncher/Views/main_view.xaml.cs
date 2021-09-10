using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using System.Threading.Tasks;

namespace SteamCMDLauncher
{
    /// <summary>
    /// Interaction logic for main_view.xaml
    /// </summary>
    public partial class main_view : Window
    {
        private static int server_count;
        private UIComponents.DialogHostContent HostDialog;

        static bool initEvents = false;
        bool out_of_date = false;

        public main_view(bool update = false)
        {
            out_of_date = update;
   
            Config.Log("[MV] Initializing UI Components");
            InitializeComponent();

            AppVersion.Text = App.Version;
            
            UpdateIcon.Visibility = update ? Visibility.Visible : Visibility.Hidden;
            versionToolTip.Text = update ? "Your not running the latest version possible" : "Your running the latest version";       
        }
        
        private void UpdateRefreshButton()
        {
            MaterialDesignThemes.Wpf.PackIcon refreshButtonIcon = (MaterialDesignThemes.Wpf.PackIcon)RefreshServers.Content;

            refreshButtonIcon.Kind = (server_count > 0) ?
                MaterialDesignThemes.Wpf.PackIconKind.Restart :
                MaterialDesignThemes.Wpf.PackIconKind.RestartOff;

            RefreshServers.IsEnabled = server_count > 0;

            refreshButtonIcon = null;
        }

        private void LoadServerFolder(string id, string location)
        {
            Config.Log("LoadServerFolder was invoked");
            string folder_location = string.Empty;

            if (!System.IO.Directory.Exists(location))
            {
                HostDialog.OKDialog("Please select the new location of the server as the last one wasn't found or exists");

                folder_location = Config.GetFolder(string.Empty, string.Empty);
                if (folder_location.Length > 0)
                {
                    Config.ChangeServerFolder(id, location, folder_location);

                    Config.Log($"Changed folder from: '{location}' to '{folder_location}'");

                    HostDialog.OKDialog("A restart is required to make full effect - Refreshing will not solve this.");
                }
                else { return; }
            }
            else
            {
                Config.Log($"Loading folder for server: {id}");
                System.Diagnostics.Process.Start("explorer.exe", location); 
            }

            folder_location = null;
        }

        private void LoadServerView(string id)
        {
            Config.Log("LoadServerView was invoked");

            Component.Struct.ServerCardInfo[] servers = Config.GetServers();

            if (servers is null)
            {
                Config.Log("[LSV] 'server' array was empty, this shouldn't be the case!");
                HostDialog.OKDialog("Internal Problem - Not cached servers, fault with server dictionary");
                return;
            }

            Config.Log("[LSV] Getting server information based from id chosen");
            Component.Struct.ServerCardInfo current_server = servers.FirstOrDefault(x => x.Unique_ID == id);
            Config.Log("[LSV] Getting server information based was given");

            if (current_server.IsEmpty)
            {
                Config.Log("[LSV] Getting server information returned nothing, something went wrong there.");
                
                HostDialog.OKDialog($"Problems attempting to find server id: {id}.\nThis is either a code fault or a database fault.");
                
                return;
            }

            if (!System.IO.Directory.Exists(current_server.Folder))
            {
                Config.Log($"[LSV] '{id}' folder has been changed since last stored, please assign the new location before running again.");
                HostDialog.OKDialog($"That server location ({current_server.Folder}) doesn't exist anymore!\nCorrect it but stating the new location from 'View Folder' button");
                return;
            }

            Config.Log("[LSV] Generating the server window");
            
            ServerView server_window = new ServerView(id, current_server.Alias, current_server.Folder, current_server.GameID);
            
            servers = null;
            
            if (server_window.IsReady)
            {
                Config.Log("[LSV] Clearing up main window objects");

                App.CancelClose = true;

                int size = ServerStack.Children.Count;
                while (ServerStack.Children.Count > 0)
                {
                    ServerStack.Children.RemoveAt(0);
                }
 
                ServerStack = null;

                Config.Log("[LSV] Clearing up main window events/hooks");
                HostDialog.Destory();
                HostDialog = null;
                UpdateIcon = null;
                Component.EventHooks.UnhookServerCardEvents();
                initEvents = false;

                Config.Log("[LSV] Force running the GCC");
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                Config.Log("[LSV] Showing the window to client");
                App.WindowClosed(this);
                App.WindowOpen(server_window);
            }
            else
            {
                Config.Log($"[LSV] Window was not ready: {server_window.NotReadyReason}");
                HostDialog.OKDialog(server_window.NotReadyReason);
            }
        }

        private void PopulateCards()
        {
            // Create a card instance
            UIComponents.ServerCard Card = new UIComponents.ServerCard();

            if(!initEvents)
            {
                Config.Log("[PC] Hooking Button Events");
                Component.EventHooks.View_Server += LoadServerView;
                Component.EventHooks.View_Folder += LoadServerFolder;
                initEvents = true;
            }

            Component.Struct.ServerCardInfo[] svr_l = Config.GetServers();
            server_count = svr_l.Length;

            ServerStack.VerticalAlignment = (server_count == 0) ? VerticalAlignment.Top : VerticalAlignment.Center;

            // Loop over each record stored
            if (server_count == 0)
            {
                Config.Log("[PC] 'server' was empty, showing no servers were found to client");

                // Textblock which shows if no servers were found
                TextBlock text = new TextBlock()
                {
                    Text = "No Servers Were Found - Add Some!",
                    FontWeight = FontWeights.DemiBold, FontSize = 20, Foreground = new SolidColorBrush(Colors.White)
                };

                ServerStack.Children.Add(text);

                // Dereference the object as we don't need it anymore
                Card = null; text = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                return;
            }

            for (int i = 0; i < server_count; i++)
            {
                Config.Log($"[PC] Loading Server Card for {svr_l[i].Unique_ID}");
                ServerStack.Children.Add(Card.CreateCard(Config.GetGameByAppId(svr_l[i].GameID), svr_l[i].Alias, svr_l[i].Folder, svr_l[i].Unique_ID));
            }

            // Dereference the object as we don't need it anymore
            Card = null;
            svr_l = null;

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
            // Tell the program we're not closing fully, we just want to show another window.
            App.CancelClose = true;

            App.WindowClosed(this);
            App.WindowOpen(new Setup(false));
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

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // App closing after select new server fix
            if (App.CancelClose)
                App.CancelClose = false;

            Config.Log("[MV] Window has been fully loaded");

            if (out_of_date)
            {
                Config.Log("[MV] Showing update dialog to the client");

                await Task.Run(() =>
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        HostDialog.YesNoDialog("Update is due",
                        "The program identified your not running the latest version possible.\nWould you like to view the page to get the latest version?",
                        new Action(() =>
                        {
                            // On "Yes" Button press
                            Config.Log("[MV] Client has approved to visit GitHub page from 'explorer.exe' - executing...");
                            System.Diagnostics.Process.Start("explorer.exe", "https://github.com/TheE7Player/SteamCMDLauncher/releases");
                        }));
                        Config.Log("[MV] Update dialog is now closed");
                    });
                });
            }

            Config.Log("[MV] Loaded Main Window");

            Config.Log("[MV] Setting up dialog host");
            HostDialog = new UIComponents.DialogHostContent(RootDialog, true, true);

            Config.Log("[MV] Populating Cards");
            PopulateCards();

            UpdateRefreshButton();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            App.WindowClosed(this);
        }
    }
}
