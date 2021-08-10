using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace SteamCMDLauncher.UIComponents
{
    public class GameSettingControl : ISettingControl
    {
        public enum ControlType
        {
            Input, Box, Pass
        }

        public string Heading { get ; set ; }
        public string Hint { get; set; }
        public string Command { get; set; }
        public string defaultValue { get; set; }
        public bool canBeBlank { get; set; }

        private string alert_message;
        private Control ctrl;
        private ControlType cType;

        public GameSettingControl(Dictionary<string, string> control)
        {
            if (control.ContainsKey("text"))
                Heading = control["text"];

            if (control.ContainsKey("alert"))
                alert_message = control["alert"];
            
            if(control.ContainsKey("type"))
            {
                switch (control["type"])
                {               
                    case "input": { 
                        ctrl = new TextBox();
                        ctrl.Width = 200;
                        cType = ControlType.Input;
                        break; 
                    }
                    case "check": { 
                        ctrl = new CheckBox();
                        cType = ControlType.Box;
                        break;
                    };
                    case "pass":
                    {
                        ctrl = new PasswordBox();
                        cType = ControlType.Pass;
                        ctrl.Width = 200;
                        break;
                    };
                    default: break;
                }
            }

            if(control.ContainsKey("command"))
            {
                string prefex = control.ContainsKey("command_prefix") ? control["command_prefix"] : string.Empty;
                Command = $"{prefex}{control["command_prefix"]}".Trim();
            }
        }

        internal UIElement GetCompoent()
        {
            var tb = new TextBlock();
            tb.Text = Heading;
            tb.Padding = new Thickness(5,0,15,0);
            tb.FontWeight = FontWeights.DemiBold;
            tb.FontSize = 16;

            var sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.VerticalAlignment = VerticalAlignment.Center;
            sp.Margin = new Thickness(5, 0, 5, 10);

            if (cType == ControlType.Box)
            {
                sp.Children.Add(ctrl);
                sp.Children.Add(tb);
            } else
            {
                sp.Children.Add(tb);
                sp.Children.Add(ctrl);
            }

            // If the object needs to display a message
            if(!string.IsNullOrEmpty(alert_message))
            {
                var alertCard = new MaterialDesignThemes.Wpf.Card();
                
                alertCard.SetResourceReference(Control.BackgroundProperty, "PrimaryHueDarkBrush");
                alertCard.SetResourceReference(Control.BackgroundProperty, "PrimaryHueDarkForegroundBrush");
                
                alertCard.Width = 400;
                alertCard.UniformCornerRadius = 3;
                alertCard.Padding = new Thickness(3);

                alertCard.Margin = new Thickness(0, 5, 0, 10);

                alertCard.Content = new TextBlock()
                {
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14, FontWeight = FontWeights.DemiBold,
                    Text = alert_message
                };
                sp.Children.Add(alertCard);
            }

            return sp;
        }
    }
}
