using System.Windows;

namespace SteamCMDLauncher.Views
{
    /// <summary>
    /// Interaction logic for extra.xaml
    /// </summary>
    public partial class extra : Window
    {
        public bool DeleteDB { get; set; }
        public bool ClearLog { get; set; }
        public bool DeleteTemp { get; set; }

        public bool isTempEnabled { get; set; }

        private UIComponents.DialogHostContent dh;

        public extra()
        {
            App.CancelClose = true;
            InitializeComponent();

            isTempEnabled = false;

            if(System.IO.Directory.Exists(Component.Archive.CACHE_PATH))
            {
                isTempEnabled = System.IO.Directory.GetFiles(Component.Archive.CACHE_PATH).Length > 0;
            }

            DialogTitle.Text = $"SteamCMDLauncher V{App._version} Advanced Menu";
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

            if(DeleteTemp)
            {
                System.IO.Directory.Delete(Component.Archive.CACHE_PATH, true);
            }

            Exit();
        }

        private void Exit()
        {
            dh.Destory();
            dh = null;
            App.WindowClosed(this);
        }

        private void ReturnButton_Click(object sender, RoutedEventArgs e)
        {
            Exit();
        }
    }
}