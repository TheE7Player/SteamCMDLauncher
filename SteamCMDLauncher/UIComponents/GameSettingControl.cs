using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SteamCMDLauncher.UIComponents.GameSettingComponent;
using System.Linq;

namespace SteamCMDLauncher.UIComponents
{
    public class GameSettingControl : ISettingControl
    {
        public string Heading { get ; set ; }
        public string Hint { get; set; }
        public string Command { get; set; }
        public string defaultValue { get; set; }
        public string PlaceHolder { get; set; }
        
        public bool canBeBlank { get; set; }
        public double Width { get; set; }

        // Extra properties that don't go along side ISettingControl Properties
        public string name, blank_error, tag;

        private string alert_message;

        private ISettingConstruct ctrl;

        // Destructor
        ~GameSettingControl()
        {
            // Dereferencing all string/reference data types
            Heading = null;
            Hint = null;
            Command = null;
            defaultValue = null;
            PlaceHolder = null;
            name = null;
            blank_error = null;
            alert_message = null;

            // Unhooking the event
            Component.EventHooks.UnhookHint();

            if(ctrl != null) 
            {
                // Dereference any extra references in the object
                string t_type = ctrl.GetControlType;
                bool successful = false;
                
                switch (t_type)
                {
                    case "gsinput": GetControlAsInstance<GSInput>().Discard(); successful = true; break;
                    case "gscheck": GetControlAsInstance<GSCheck>().Discard(); successful = true; break;
                    case "gscombo": GetControlAsInstance<GSCombo>().Discard(); successful = true; break;
                    case "gspass": GetControlAsInstance<GSPass>().Discard(); successful = true;  break;
                }

                if(!successful)
                    throw new Exception($"[GSC] Unknown control type to parse to: {ctrl.GetControlType}");

                t_type = null;
                // Dereference the control
                ctrl = null;
            }
        }

        public GameSettingControl()
        {

        }

        public GameSettingControl(string name, Dictionary<string, string> control)
        {
            this.name = name;
            
            SetDefaults(control);

            if (control.ContainsKey("tag"))
                this.tag = control["tag"];

            if (control.ContainsKey("type"))
            {
                switch (control["type"])
                {
                    case "input": {                                 
                            ctrl = new GSInput(this);

                            if(control.ContainsKey("write_to"))
                            {
                                ((GSInput)ctrl).SetWritePath(control["write_to"]);
                            }

                        } break;
                    case "pass": ctrl = new GSPass(this); break;
                    case "combo": { 
                            ctrl = new GSCombo(this);

                            if (control.ContainsKey("dir"))
                                ((GSCombo)ctrl).SetComboDir(control["dir"]);

                            if(control.ContainsKey("combo-strict"))
                            {
                                Dictionary<string, string> dict = control["combo-strict"] // Get the string
                                .Split(new[] { '\r', '\n' }) // Split by new line (return line)
                                .Where(x => x.Length > 1) // Filter out empty space by getting > 1 len only
                                .Select(y => y.Replace("\"", "").Replace(",", "") // Remove the string quotes, and commas
                                .Split(':')) // Split based on the kv spliter
                                .ToDictionary(result => result[0].Trim(), res => res[1].Trim()); // Then turn the split into a dictionary
                                
                                ((GSCombo)ctrl).SetStrictValues(new Dictionary<string, string>(dict));

                                dict.Clear(); dict = null;
                            }
                            else
                            {
                                if (control.ContainsKey("combo-target"))
                                    ((GSCombo)ctrl).SetComboPattern(control["combo-target"]);

                                if (control.ContainsKey("combo-range"))
                                    ((GSCombo)ctrl).SetNumberRange(control["combo-range"]);
                            }
                        } break;
                    case "check": {
                            string[] val = null;
                        
                            if(control.ContainsKey("returns"))
                            {
                               val = control["returns"].Split(',').Select(x => x.Trim()).ToArray();
                               ctrl = new GSCheck(this, (val.Length>= 2) ? val : null);
                            } 
                            else
                            {
                               Config.Log($"[GSM] Checkbox '{Heading}' doesn't return any true or false state, consider a different component!");
                               ctrl = new GSCheck(this);
                            }

                            break;
                    }
                    default:
                        Config.Log($"[GSM] Cannot find object instance for '{control["type"]}' - Ignoring that control all together...");
                        break;
                }
            }
        }
        
        private void SetDefaults(Dictionary<string, string> control)
        {
            if (control.ContainsKey("text"))
                Heading = control["text"];

            if (control.ContainsKey("alert"))
                alert_message = control["alert"];

            if (control.ContainsKey("hint"))
                Hint = control["hint"];

            if (control.ContainsKey("default"))
                defaultValue = control["default"];

            if (control.ContainsKey("placeholder"))
                PlaceHolder = control["placeholder"];

            if (control.ContainsKey("width"))
            {
                if (!Double.TryParse(control["width"], out double w))
                {
                    Config.Log($"[GSM] Cannot accept width of '{control["width"]}' - Ignoring size...");
                    Width = -1;
                    return;
                }
                Width = w;
            }
            
            if(control.ContainsKey("command"))
            {
                string prefex = control.ContainsKey("command_prefix") ? control["command_prefix"] : string.Empty;
                Command = $"{prefex}{control["command"]}".Trim();
                prefex = null;
            }

            if(control.ContainsKey("can_leave_blank"))
            {
                canBeBlank = (control["can_leave_blank"] == "True");
            }

            if (control.ContainsKey("blank_alert"))
                blank_error = control["blank_alert"];

            
        }

        private void SetGrid(UIElement ctrl, int r, int c)
        {
            Grid.SetRow(ctrl, r); Grid.SetColumn(ctrl, c);
        }

        public GameSettingControl DeepClone()
        {
            GameSettingControl new_ent = new GameSettingControl()
            {
                name = this.name,
                Heading = this.Heading,
                Hint = this.Hint,
                Command = this.Command,
                canBeBlank = this.canBeBlank,
                PlaceHolder = this.PlaceHolder,
                alert_message = this.alert_message,
                Width = this.Width,
                defaultValue = this.defaultValue,
                blank_error = this.blank_error,
                tag = this.tag
            };
            new_ent.ctrl = this.ctrl;
            return new_ent;
        }

        public string GetArg(string info = null) => ctrl.GetParam(info);

        public bool IsEmpty() => ctrl.IsEmpty;

        public string SaveValue() => ctrl.SaveValue;

        public string GetStringType() => ctrl.GetControlType;

        public T GetControlAsInstance<T>() => (T)ctrl;
        
        public void LoadValue(string val) => ctrl.LoadValue(val);

        internal UIElement GetComponent()
        {
            if (ctrl is null) return null;

            Control r_ctrl = ctrl.GetComponent();

            TextBlock tb = new TextBlock();
            tb.Text = Heading;
            tb.Padding = new Thickness(5,0,15,0);
            tb.FontWeight = FontWeights.DemiBold;
            tb.FontSize = 16;
            tb.VerticalAlignment = VerticalAlignment.Center;

            Grid sp = new Grid();
            
            sp.VerticalAlignment = VerticalAlignment.Center;
            sp.Margin = new Thickness(5, 0, 5, 10);

            sp.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(20, GridUnitType.Auto) });
            sp.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(200, GridUnitType.Auto) });

            sp.RowDefinitions.Add(new RowDefinition());

            if (r_ctrl.GetType() == typeof(CheckBox))
            {
                sp.Children.Add(r_ctrl);
                sp.Children.Add(tb);

                SetGrid(tb, 0, 1);
                SetGrid(r_ctrl, 0, 0);
            }
            else
            {
                sp.Children.Add(tb);
                sp.Children.Add(r_ctrl);

                SetGrid(tb, 0, 0);
                SetGrid(r_ctrl, 0, 1);
            }

            // Add Hint button (if any hints)
            if(!string.IsNullOrEmpty(Hint))
            {
                Button btn = new Button();
                btn.Margin = new Thickness(5, 0, 5, 0);

                MaterialDesignThemes.Wpf.PackIcon ipack = new MaterialDesignThemes.Wpf.PackIcon();
                ipack.Kind = MaterialDesignThemes.Wpf.PackIconKind.InformationOutline;

                btn.Content = ipack;

                btn.Click += (_, e) =>
                {
                    Component.EventHooks.InvokeHint(Hint);
                };

                sp.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(20, GridUnitType.Auto) });
                sp.Children.Add(btn);
                SetGrid(btn, 0, 2);
            }

            // If the object needs to display a message
            if (!string.IsNullOrEmpty(alert_message))
            {
                MaterialDesignThemes.Wpf.Card alertCard = new MaterialDesignThemes.Wpf.Card();
                
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
