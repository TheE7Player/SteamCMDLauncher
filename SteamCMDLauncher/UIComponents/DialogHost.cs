using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;

namespace SteamCMDLauncher.UIComponents
{
    public class DialogHostContent
    {
        private DialogHost _dialog;
        private DispatcherFrame df;
        private bool? result;
        private bool isWaiting;
        private bool forceOpenWhenCall = false;

        /// <summary>
        /// Setups a object to create a programatic dialoghost
        /// </summary>
        /// <param name="dialog">The dialog to load the components from</param>
        /// <param name="waitForAction">If this is used to get user input, if so it will wait for a result (bool)</param>
        /// <param name="openOnCall">If when calling any form of dialog method, to open once finished (false by default)</param>
        public DialogHostContent(DialogHost dialog, bool waitForAction = true, bool openOnCall = false)
        {
            this._dialog = dialog;

            this.isWaiting = waitForAction;
            this.forceOpenWhenCall = openOnCall;

            if(this.isWaiting)
                df = new DispatcherFrame(true);
        }

        /// <summary>
        /// Changes an element in the dialog (IF) the element supports ".Text" property
        /// </summary>
        /// <param name="name">The element is a name (ASSIGNED) to target</param>
        /// <param name="context">The text to change it to</param>
        /// <returns></returns>
        public bool ChangePropertyText(string name, string context)
        {
            if (this._dialog is null) return false;

            var components = ((StackPanel)this._dialog.DialogContent).Children.OfType<FrameworkElement>();

            dynamic obj = null;
            
            foreach (FrameworkElement component in components)
            {
                
                if (!component.Name.Same(name)) continue;

                obj = component;
                
                try
                {
                    obj.Text = context;
                    
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return false;
        }

        public int GetResult()
        {
            if (result == null) return -1;

            bool initialValue = (bool)result;
           
            result = null;

            return Convert.ToInt32(initialValue);
        }

        private void ExitState()
        {
            if (this.isWaiting)
            {
                df.Continue = false;

                df = new DispatcherFrame(true);
            }

            _dialog.IsOpen = false;       
        }

        public void ShowDialog()
        {
            if(this._dialog.IsOpen)
                this._dialog.IsOpen = false;

            this._dialog.IsOpen = true;

            if(isWaiting)
                System.Windows.Threading.Dispatcher.PushFrame(df);
        }

        public void CloseDialog() => ExitState();

        public void GameInstallDialog(string game)
        {
            var stp = new StackPanel();
            stp.Margin = new Thickness(20);

            TextBlock gameText = new TextBlock()
            {
                Text = $"Now Installing:{Environment.NewLine}{game}", FontWeight = FontWeights.Bold,
                FontSize = 14, TextWrapping = TextWrapping.Wrap, 
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };

            ProgressBar pg = new ProgressBar()
            {
                Value = 0,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsIndeterminate = true,
                Margin = new Thickness(0, 20, 0, 25)
            };

            // Turn the progressbar into a circular one
            pg.SetResourceReference(Control.StyleProperty, "MaterialDesignCircularProgressBar");
       
            TextBlock status = new TextBlock()
            {
                Text = "Depending on file size and bandwidth, this may take a while",
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontStyle = FontStyles.Italic,
                FontSize = 10,
                FontWeight = FontWeights.Bold
            };

            TextBlock text = new TextBlock()
            {
                Name = "GameInstallStatus",
                Text = "...",
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };

            stp.Children.Add(gameText);
            stp.Children.Add(pg);
            stp.Children.Add(status);
            stp.Children.Add(text);

            _dialog.DialogContent = stp;

            if (forceOpenWhenCall) ShowDialog();
        }

        public void YesNoDialog(string title, string message)
        {
            Button Yes, No;
            
            var MainPanel = new StackPanel();
            
            var ButtonPanel = new StackPanel();

            ButtonPanel.Orientation = Orientation.Horizontal;
            
            ButtonPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            
            ButtonPanel.Margin = new System.Windows.Thickness(0,10,0,5);

            MainPanel.Margin = new System.Windows.Thickness(20, 20, 20, 20);

            MainPanel.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0,4,0,2) });
            
            MainPanel.Children.Add(new Separator() { Margin = new Thickness(0, 0, 0, 5) });
            
            MainPanel.Children.Add(new TextBlock { Text = message, FontSize = 16, TextWrapping = TextWrapping.Wrap });

            Yes = new Button { Content = "Yes", Margin = new Thickness(0, 0, 5, 0) };

            No = new Button { Content = "No", Margin = new Thickness(5, 0, 0, 0) };

            Yes.Click += (s, e) => { result = true; ExitState(); }; 
            No.Click += (s, e) => { result = false; ExitState(); };

            ButtonPanel.Children.Add(Yes);

            ButtonPanel.Children.Add(No);

            MainPanel.Children.Add(ButtonPanel);

            _dialog.DialogContent = MainPanel;

            if (forceOpenWhenCall) ShowDialog();
        }

        public void OKDialog(string message)
        {
            Button Ok;

            var MainPanel = new StackPanel();
            
            MainPanel.Margin = new System.Windows.Thickness(20, 20, 20, 20);

            Ok = new Button { Content = "OK", Width = 80 };

            Ok.Click += (s, e) => { ExitState(); };

            MainPanel.Children.Add(new TextBlock { Text = message, FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 8) });

            MainPanel.Children.Add(Ok);

            _dialog.DialogContent = MainPanel;

            if (forceOpenWhenCall) ShowDialog();
        }

        public void Destory()
        {
            df = null;
            _dialog = null;
            result = null;
        }

    }
}
