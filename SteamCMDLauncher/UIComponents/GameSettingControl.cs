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

        public delegate void view_hint_dialog(string hint);
        public event view_hint_dialog View_Dialog;

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

            if (control.ContainsKey("hint"))
                Hint = control["hint"];

            if (control.ContainsKey("type"))
            {
                switch (control["type"])
                {               
                    case "input": { 
                        ctrl = new TextBox();
                        cType = ControlType.Input;

                        if (control.ContainsKey("default"))
                            ((TextBox)ctrl).Text = control["default"];

                        if (control.ContainsKey("placeholder"))
                        {
                            ((TextBox)ctrl).SetValue(MaterialDesignThemes.Wpf.HintAssist.HintProperty, control["placeholder"]);
                        }

                        if (control.ContainsKey("width"))
                        {
                            if(!Double.TryParse(control["width"], out double w))
                            {
                               Config.Log($"[GSM] Cannot accept width of '{control["width"]}' - Ignoring size...");
                            }

                            ((TextBox)ctrl).Width = w;
                        }
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

                        if (control.ContainsKey("placeholder"))
                        {
                            ((PasswordBox)ctrl).SetValue(MaterialDesignThemes.Wpf.HintAssist.HintProperty, control["placeholder"]);
                        }

                        if (control.ContainsKey("width"))
                        {
                            if (!Double.TryParse(control["width"], out double w))
                            {
                                Config.Log($"[GSM] Cannot accept width of '{control["width"]}' - Ignoring size...");
                            }

                            ((PasswordBox)ctrl).Width = w;
                        }

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

        private void SetGrid(UIElement ctrl, int r, int c)
        {
            Grid.SetRow(ctrl, r); Grid.SetColumn(ctrl, c);
        }

        internal UIElement GetCompoent()
        {
            var tb = new TextBlock();
            tb.Text = Heading;
            tb.Padding = new Thickness(5,0,15,0);
            tb.FontWeight = FontWeights.DemiBold;
            tb.FontSize = 16;
            tb.VerticalAlignment = VerticalAlignment.Center;

            var sp = new Grid();
            
            sp.VerticalAlignment = VerticalAlignment.Center;
            sp.Margin = new Thickness(5, 0, 5, 10);
            //sp.ShowGridLines = true;

            sp.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(20, GridUnitType.Auto) });
            sp.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(200, GridUnitType.Auto) });

            sp.RowDefinitions.Add(new RowDefinition());
        
            if (cType == ControlType.Box)
            {
                sp.Children.Add(ctrl);
                sp.Children.Add(tb);
                
                SetGrid(tb, 0, 1);
                SetGrid(ctrl, 0, 0);
            } 
            else
            {
                sp.Children.Add(tb);
                sp.Children.Add(ctrl);
                
                SetGrid(tb, 0, 0);
                SetGrid(ctrl, 0, 1);
            }

            // Add Hint button (if any hints)
            if(!string.IsNullOrEmpty(Hint))
            {
                var btn = new Button();
                btn.Margin = new Thickness(5, 0, 5, 0);

                var ipack = new MaterialDesignThemes.Wpf.PackIcon();
                ipack.Kind = MaterialDesignThemes.Wpf.PackIconKind.InformationOutline;

                btn.Content = ipack;

                btn.Click += (_, e) =>
                {
                    View_Dialog.Invoke(Hint);
                };

                sp.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(20, GridUnitType.Auto) });
                sp.Children.Add(btn);
                SetGrid(btn, 0, 2);
            }

            // If the object needs to display a message
            if (!string.IsNullOrEmpty(alert_message))
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

                sp.RowDefinitions.Add(new RowDefinition());

                sp.Children.Add(alertCard);

                SetGrid(alertCard, 1, 0);
                Grid.SetColumnSpan(alertCard, 3);
            }

            return sp;
        }
    }
}
