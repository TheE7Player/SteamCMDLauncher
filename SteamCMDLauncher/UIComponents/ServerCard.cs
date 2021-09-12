using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SteamCMDLauncher.UIComponents
{
    public class ServerCard
    {
        #region Padding and Margins
        private Thickness card_padding = new Thickness(2, 10, 2, 5);
        private Thickness card_margin = new Thickness(10, 0, 10, 0);

        private Thickness btn_folder_margin = new Thickness(75, 5, 75, 10);
        private Thickness btn_view_margin = new Thickness(100, 0, 100, 0);

        private Thickness pnl_game_title_margin = new Thickness(4, 0, 4, 10);
        private Thickness pnl_server_alias_margin = new Thickness(4, 0, 4, 40);

        private Thickness sep_margin = new Thickness(0, 0, 0, 20);

        private Thickness txt_game_margin = new Thickness(4, 0, 4, 10);
        private Thickness txt_alias_margin = new Thickness(4, 0, 4, 40);
        
        private Thickness ico_margin = new Thickness(0, 0, 0, 10);
        
        #endregion

        #region Cached variables
        private string btn_view_text = "View";
        private string btn_folder_text = "View Folder";
        
        private int server_count = 1;
        private const double WIDTH = 300;
        private const double HEIGHT = 200;
        private const double ICON_SIZE = 25;
        #endregion

        #region Constructor & Destructor 
        public ServerCard()
        {
            // Reset the server count if need be
            if (server_count > 1) server_count = 1;
        }

        ~ServerCard()
        {
            btn_view_text = null;
            btn_folder_text = null;
        }
        #endregion

        #region Events
        private void ChangeCursor(object sender, MouseEventArgs e)
        {
            MaterialDesignThemes.Wpf.PackIcon self = (MaterialDesignThemes.Wpf.PackIcon)sender;

            self.Cursor = self.Cursor != Cursors.Hand ? Cursors.Hand : Cursors.Arrow;

            self = null;
            e = null;
            sender = null;
        }

        private void ButtonClick(object sender, RoutedEventArgs e)
        {
            Button self = (Button)sender;

            string Name = self.Name[1..];
            string Tag = self.Tag as string;

            Component.EventHooks.InvokeServerCard(Name, Tag);

            Name = null;
            Tag = null;

            self = null;
            sender = null;
            e = null;
        }

        private void Cleanup(object sender, RoutedEventArgs e)
        {
            MaterialDesignThemes.Wpf.Card card = (MaterialDesignThemes.Wpf.Card)sender;

            // Clear tag, which may contain extra information
            card.Tag = null;

            // Dispose all the panel children
            StackPanel children = card.Content as StackPanel;

            /*
             *  Len(6):
                [0] gameName : text
                [1] seperator : sep
                [2] serverName : text
                [3] icon : PackIcon
                [4] viewButton : button
                [5] viewServer : button
            */

            TextBlock name = (TextBlock)children.Children[0],
            svr_name = (TextBlock)children.Children[2];

            Button folder = (Button)children.Children[4],
            svr_view = (Button)children.Children[5];

            MaterialDesignThemes.Wpf.PackIcon icon = (MaterialDesignThemes.Wpf.PackIcon)children.Children[3];

            Separator sep = (Separator)children.Children[1];

            name.Text = null;
            svr_name = null;

            icon.MouseLeave -= ChangeCursor;
            icon.MouseEnter -= ChangeCursor;
            icon.ToolTip = null;

            folder.Name = null;
            svr_view.Name = null;

            folder.Tag = null;
            svr_view.Tag = null;

            folder.Click -= ButtonClick;
            svr_view.Click -= ButtonClick;

            name = null;
            svr_name = null;
            folder = null;
            svr_view = null;
            icon = null;
            sep = null;

            children.Children.RemoveRange(0, 6);

            children = null;

            card.Unloaded -= Cleanup;

            card = null;

            sender = null;
            e = null;
        }
        #endregion

        public MaterialDesignThemes.Wpf.Card CreateCard(string game, string alias, string folder, string _id)
        {
            // Setup the objects first
            MaterialDesignThemes.Wpf.Card card = new MaterialDesignThemes.Wpf.Card();
            MaterialDesignThemes.Wpf.PackIcon icon = new MaterialDesignThemes.Wpf.PackIcon();
            
            StackPanel panel = new StackPanel();
            Button viewButton, viewServer;
            TextBlock gameName, serverName;
            Separator seperator = new Separator { Margin = this.sep_margin };

            // Set the card size (important)
            card.Width = WIDTH; card.Height = HEIGHT;

            // Setting up the padding and margin as well
            card.Margin = card_margin; card.Padding = card_padding;

            // Assign a tag to make finding location easier
            card.Tag = folder;

            // Setting up titles
            gameName = new TextBlock
            {
                Text = game,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = txt_game_margin
            };
            
            string fixed_string = null;

            if (!string.IsNullOrEmpty(alias))
            {
                if (alias.Length > 34)
                    fixed_string = $"{alias.Substring(0, 34)}...";
                else if (alias.Length > 0)
                    fixed_string = alias;
                else
                    fixed_string = $"Server-{server_count++}";
            } 
            else
            {
                fixed_string = $"Server-{server_count++}";
            }

            serverName = new TextBlock
            {
                Text = fixed_string,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = txt_game_margin
            };

            // Now we setup the buttons

            // Fix if the ID starts with number, this prevents "not a valid value for property 'Name'" exception
            string modified_id = $"S{_id}";

            viewButton = new Button { Content = btn_folder_text, Margin = btn_folder_margin, Name = modified_id, Tag = folder };
            viewServer = new Button { Content = btn_view_text, Margin = btn_view_margin, Name = modified_id };

            modified_id = null;

            // Add an event to the buttons
            viewButton.Click += ButtonClick;
            viewServer.Click += ButtonClick;

            bool folder_exists = System.IO.Directory.Exists(folder);

            // Set the icon depending if the folder exists
            icon.Kind = (folder_exists) ? 
                MaterialDesignThemes.Wpf.PackIconKind.Link :                                                          
                MaterialDesignThemes.Wpf.PackIconKind.LinkVariantOff;

            icon.Width = ICON_SIZE; icon.Height = ICON_SIZE;
            icon.HorizontalAlignment = HorizontalAlignment.Center;

            icon.ToolTip = new ToolTip { Content = (folder_exists) ? "Folder still exists" : $"Couldn't find '{folder}'" };
            icon.Padding = ico_margin;

            // These events change the cursor type
            icon.MouseEnter += ChangeCursor;
            icon.MouseLeave += ChangeCursor;

            card.Unloaded += Cleanup;
            
            // Now we sequentially add the items into the panel
            panel.Children.Add(gameName);
            panel.Children.Add(seperator);
            panel.Children.Add(serverName);
            panel.Children.Add(icon);
            panel.Children.Add(viewButton);
            panel.Children.Add(viewServer);

            // Append the panel to the card
            card.Content = panel;

            // Do Any Cleanup
            fixed_string = null;
            icon = null;
            panel = null;
            viewButton = null; viewServer = null;
            gameName = null; serverName = null;
            seperator = null;

            game = null; alias = null; folder = null; _id = null;

            // Finish, time to return
            return card;
        }
    }
}
