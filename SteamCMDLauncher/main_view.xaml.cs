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
            PopulateCards();
        }

        private void PopulateCards()
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
            var setup = new Setup();
            this.Close();
            setup.Show();
        }
    }
}
