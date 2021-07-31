using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
    /// Interaction logic for ServerView.xaml
    /// </summary>
    public partial class ServerView : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            Console.Beep();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.alias)));
        }

        public string id { get; private set; }
        
        private string alias;
        public string Alias { 
            get {
                return alias;
            }
            
            private set 
            {
                alias = value;
                OnPropertyChanged();
            }
        }

        public ServerView(string id, string alias)
        {
            InitializeComponent();
            this.DataContext = this;
            
            this.id = id;
            this.alias = alias;          
        }

        // Hint: -> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(outputFolderPath)));

        private void ReturnBack_Click(object sender, RoutedEventArgs e)
        {
            this.id = null;
            this.alias = null;
            this.Close();
        }
    }
}
