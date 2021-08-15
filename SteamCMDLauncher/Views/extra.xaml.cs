using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SteamCMDLauncher.Views
{
    /// <summary>
    /// Interaction logic for extra.xaml
    /// </summary>
    public partial class extra : Window
    {
        public bool DeleteDB { get; set; }
        public bool ClearLog { get; set; }

        private UIComponents.DialogHostContent dh;

        public extra()
        {
            InitializeComponent();
            dh = new UIComponents.DialogHostContent(RootDialog, true, true);
            this.DataContext = this;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if(ClearLog)
            {
                Config.Log("Clearing logs...");

                if (Config.CleanLog())
                    Config.Log("Clearing log was successful");
                else
                    dh.OKDialog("Clearing log was unsuccessful or there were no logs to delete.");
            }

            if(DeleteDB)
            {
                if (Config.DatabaseExists)
                {
                    System.IO.File.Delete(Config.DatabaseLocation);
                    Config.Log("Database was been erased - no trace was left. Starting brand new.");
                }
                else
                {
                    dh.OKDialog("Database wasn't erased - as there were no database to begin with.");
                }
            }

            Exit();
        }

        private void Exit()
        {
            dh.Destory();
            dh = null;
            this.Close();
        }

        private void ReturnButton_Click(object sender, RoutedEventArgs e)
        {
            Exit();
        }
    }
}