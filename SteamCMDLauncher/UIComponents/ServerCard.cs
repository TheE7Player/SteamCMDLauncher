using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SteamCMDLauncher.UIComponents
{
    public class ServerCard
    {
        #region Padding and Margins
        private readonly Thickness card_padding = new Thickness(2, 10, 2, 5);
        private readonly Thickness card_margin = new Thickness(10, 0, 10, 0);

        private readonly Thickness btn_folder_margin = new Thickness(75, 5, 75, 10);
        private readonly Thickness btn_view_margin = new Thickness(100, 0, 100, 0);

        private readonly Thickness pnl_game_title_margin = new Thickness(4, 0, 4, 10);
        private readonly Thickness pnl_server_alias_margin = new Thickness(4, 0, 4, 40);

        private readonly Thickness sep_margin = new Thickness(0, 0, 0, 20);

        private readonly Thickness txt_game_margin = new Thickness(4, 0, 4, 10);
        private readonly Thickness txt_alias_margin = new Thickness(4, 0, 4, 40);
        
        private readonly Thickness ico_margin = new Thickness(0, 0, 0, 10);
        
        #endregion

        #region Cached variables
        private const string btn_view_text = "View";
        private const string btn_folder_text = "View Folder";
        
        private int server_count = 1;
        private readonly double WIDTH = 300;
        private readonly double HEIGHT = 200;
        private readonly double ICON_SIZE = 25;
        #endregion

        #region Events/Delegates
        // Function which describes how the event is triggered with arguments
        public delegate void view_server_func(string unique_id, string alias);

        // Function which describes how the event triggers to open the server folder
        public delegate void view_folder(string unique_id, string location);

        // Event handler which prompts the invoke
        public event view_server_func View_Server;
        public event view_folder View_Folder;
        #endregion

        public ServerCard()
        {
            // Reset the server count if need be
            if (server_count > 1) server_count = 1;
        }

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

            viewButton = new Button { Content = btn_folder_text, Margin = btn_folder_margin };
            viewServer = new Button { Content = btn_view_text, Margin = btn_view_margin };

            // Add an event to the buttons
            viewButton.Click += (_, e) => { View_Folder?.Invoke(_id, folder);};
            viewServer.Click += (_, e) => { View_Server?.Invoke(_id, fixed_string); };
            
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
            icon.MouseEnter += (_, e) => { icon.Cursor = Cursors.Hand; };
            icon.MouseLeave += (_, e) => { icon.Cursor = Cursors.Arrow; };

            // Now we sequently add the items into the panel
            panel.Children.Add(gameName);
            panel.Children.Add(seperator);
            panel.Children.Add(serverName);
            panel.Children.Add(icon);
            panel.Children.Add(viewButton);
            panel.Children.Add(viewServer);

            // Append the panel to the card
            card.Content = panel;

            // Finish, time to return
            return card;
        }
    }
}
