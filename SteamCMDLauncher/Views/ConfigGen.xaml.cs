using Newtonsoft.Json.Linq;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;
using System.Collections.Generic;

namespace SteamCMDLauncher.Views
{
    /// <summary>
    /// Interaction logic for ConfigGen.xaml
    /// </summary>
    public partial class ConfigGen : Window
    {
        private int priorityControlLevel = 0;
        private bool disposed = false;
       
        private char lastControlType = '\0';
        
        private UIComponents.DialogHostContent dh;
        private Lazy<DataTable> table;
        private Lazy<Dictionary<string, Dictionary<string, string[]>>> output_directory;

        //private JObject output;
        //private JObject lang_output;

        public ConfigGen()
        {
            InitializeComponent();
        }

        #region Priority Logic
        private void PrioritySet()
        {
            // Check if a name is set
            if (ControlName.Text.Length < 1)
            {
                priorityControlLevel = 0;
                return;
            }

            // Name must be set, turn it to 1
            priorityControlLevel = 1;

            // If the control type or category isn't set, stop here.
            if(ControlType.SelectedIndex == -1 || ControlCategory.SelectedIndex == -1)
            {
                return;
            }

            // Type and Category must be set, turn it to 2
            priorityControlLevel = 2;

            // End here, as extra details is optional step (not needed leveled) 
        }

        private void PriorityChanged()
        {
            // Set the priority level first
            PrioritySet();

            // Set the controls accordantly based on level
            bool comboCondition = priorityControlLevel >= 1;
            bool extraCondition = priorityControlLevel >= 2;
            
            ControlType.IsEnabled = comboCondition;
            ControlCategory.IsEnabled = comboCondition;

            ControlExtra.IsEnabled = extraCondition;

            GetExtraControls(!extraCondition);
        }
        #endregion

        #region Control Events

        private bool AddingNewCategory = false;

        private void NewCategoryAdd(string input)
        {
            bool name_valid = true;

            int letter_len = input.Length;
            
            // Get the highest index possible ( n - 1 )
            int old_len = ControlCategory.Items.Count - 1;
            
            char[] letters = input.ToCharArray();

            if (letter_len < 2)
            {
                //TODO: Fix where this dialog shows but cannot activate again?
                dh.OKDialog("The given name was too short. Please pick a better name for this category.");
                letters = null;
                return;
            }

            for (int i = 0; i < letter_len; i++)
            {
                if (!char.IsLetter(letters[i])) { name_valid = false; break; }
            }

            if (!name_valid)
            {
                dh.OKDialog($"The name \"{input}\" was invalid to use:\nThe key name must ONLY have letters - This means no numbers, symbols or spaces (use underscore for spacing)");
            }
            else
            {
                // Get the last element - this is used to assign a new category
                ComboBoxItem add_item = (ComboBoxItem)ControlCategory.Items.GetItemAt(old_len);

                // Create the new element to be added
                ComboBoxItem new_item = new ComboBoxItem { Content = input };

                AddingNewCategory = true;

                // Remove the last element from the control
                ControlCategory.Items.RemoveAt(old_len);

                // Add the newest category to the list
                ControlCategory.Items.Add(new_item);

                // And we insert the add category to the bottom (latest addition)
                ControlCategory.Items.Add(add_item);

                // Then we deference the items in heap so the GC can collect it up
                add_item = null;
                new_item = null;

                ControlCategory.SelectedIndex = old_len;
            }

            letters = null;

        }

        private void RemoveCategory(ComboBoxItem item)
        {
            AddingNewCategory = true;

            ControlCategory.Items.Remove(item);
            
            item = null;
        }

        private void ControlCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            sender = null; e = null;
            int currentIndex = ControlCategory.SelectedIndex;

            if (AddingNewCategory) { AddingNewCategory = false; return; }

            if (currentIndex < 0) return;

            string tag_look_for = "Add";

            ComboBoxItem currentCombo = (ComboBoxItem)ControlCategory.Items.GetItemAt(currentIndex);
            bool AddNewCategory = currentCombo.Tag.Equals(tag_look_for);
            
            tag_look_for = null;

            if (AddNewCategory)
            {
                dh.InputDialog("Add New Category", "Please name the new category... Only Letters are allowed", NewCategoryAdd);
            }
            else
            {
                // If the user wants to delete it (Holding Left Shift while selecting)
                if( Keyboard.IsKeyDown(Key.LeftShift) )
                {
                    if(AddNewCategory)
                    {
                        dh.OKDialog("Sorry but you cannot remove 'Add New Category', this is not recommended!");
                        return;
                    }

                    dh.YesNoDialog("Delete category", $"Are you sure you want to remove \"{currentCombo.Content}\"?\nThere is no return if 'Yes'", new Action(() => { RemoveCategory(currentCombo); }));
                }
            }
            
            currentCombo = null;
            PriorityChanged();
        }

        private void ControlType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            sender = null; e = null;
            PriorityChanged();
        }

        private void ControlName_TextChanged(object sender, TextChangedEventArgs e)
        {
            sender = null; e = null;
            PriorityChanged();
        }
        #endregion

        #region Extra Options Control
        private void AutoSizeColumn(object o, RoutedEventArgs e)
        {
            DataGrid tab = (DataGrid)o;

            DataGridLength auto_size = new DataGridLength(1.0, DataGridLengthUnitType.Star);

            tab.Columns[0].Width = auto_size;
            tab.Columns[1].Width = auto_size;

            tab = null;
            o = null;
            e = null;
        }

        private void ComboStrictDialog(object o, RoutedEventArgs e)
        {
            o = null; e = null;

            int width = 700, Height = 350;

            if(!table.IsValueCreated)
            {
                table.Value.Columns.Add("Key");
                table.Value.Columns.Add("Value");
            }

            DataGrid tableOut = new DataGrid
            {
                Width = width, MaxWidth = width,
                Height = Height, MaxHeight = Height,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                ItemsSource = table.Value.AsDataView()
            };

            tableOut.Loaded += AutoSizeColumn;

            tableOut.Unloaded += (o, e) => { ((DataGrid)o).Loaded -= AutoSizeColumn; e = null; o = null; };

            dh.ShowComponent("Set Key Pairs for GSCombo", tableOut);

            tableOut.ItemsSource = null;
            tableOut = null;
        }

        private void GetExtraControls(bool clear = false)
        {

            StackPanel content = ((ScrollViewer)ControlExtra.Content).Content as StackPanel;

            if (clear)
            {
                content.Children.Clear();
                content.Children.Add(new TextBlock { Text = "No Type Was Selected" });
                lastControlType = '\0';
            }
            else
            {
                // Text, Password, Check, Combo (l)
                char[] compoent_idx = new char[] { 't', 'p', 'c', 'l' };
                
                char t = compoent_idx[ControlType.SelectedIndex];

                // Clear the controls ONLY if the control has been changed
                if (lastControlType == '\0') lastControlType = t;
                else if (lastControlType != t)
                {
                    lastControlType = t;
                    content.Children.Clear();
                }
                else
                {
                    // Ignore the creation of new objects

                    content = null;
                    compoent_idx = null;
                    return;
                }

                compoent_idx = null;

                Thickness default_space = new Thickness(0, 5, 0, 5);

                TextBox control_label = new TextBox() { Name = "text", Tag = 1, Margin = default_space };
                TextBox control_def = new TextBox() { Name = "default", Margin = default_space };
                TextBox control_hint = new TextBox() { Name = "hint", Margin = default_space };
                TextBox control_alert = new TextBox() { Name = "alert", Margin = default_space };
                TextBox control_cmd = new TextBox() { Name = "command", Tag = 1, Margin = default_space };
                TextBox control_cmd_pf = new TextBox() { Name = "command_prefix", Margin = default_space };
                
                ComboBox control_tag = new ComboBox() { Name = "tag", Margin = default_space };

                // Add the tags that go alongside this control
                control_tag.Items.Add(new ComboBoxItem { Content = "NONE" });
                control_tag.Items.Add(new ComboBoxItem { Content = "IP" });
                control_tag.Items.Add(new ComboBoxItem { Content = "PASS" });
                control_tag.Items.Add(new ComboBoxItem { Content = "PORT" });

                // This entity is read only and is invisible (collapsed) and is auto-filled
                TextBox control_type = new TextBox()
                {
                    Name = "type",
                    Margin = default_space,
                    IsEnabled = false,
                    Tag = 1,
                    Text = t == 't' ? "input" : t == 'p' ? "pass" : t == 'c' ? "check" : "combo",
                    Visibility = Visibility.Collapsed
                };

                MaterialDesignThemes.Wpf.HintAssist.SetHint(control_label, "Control Label (Title)");
                MaterialDesignThemes.Wpf.HintAssist.SetHint(control_cmd, "Command (Denoted with $)");
                MaterialDesignThemes.Wpf.HintAssist.SetHint(control_cmd_pf, "Command Prefix");
                MaterialDesignThemes.Wpf.HintAssist.SetHint(control_def, "Default value (Optional)");
                MaterialDesignThemes.Wpf.HintAssist.SetHint(control_hint, "Hint (Optional)");
                MaterialDesignThemes.Wpf.HintAssist.SetHint(control_alert, "Alert Warning (Optional)");
                MaterialDesignThemes.Wpf.HintAssist.SetHint(control_tag, "Server Argument (Optional)");

                content.Children.Add(control_type);
                content.Children.Add(control_label);
                content.Children.Add(control_cmd);
                content.Children.Add(control_cmd_pf);
                content.Children.Add(control_tag);
                content.Children.Add(control_def);
                content.Children.Add(control_hint);
                content.Children.Add(control_alert);

                control_type = null;
                control_label = null;
                control_def = null;
                control_hint = null;
                control_alert = null;
                control_cmd = null;
                control_cmd_pf = null;
                control_tag = null;

                if (t == 't' || t == 'p')
                {
                    // If the control type is an input or password

                    CheckBox blank = new CheckBox() { Content = "Can Be Blank", Name = "can_leave_blank", Margin = default_space, IsChecked = true };
                   
                    TextBox placeholder = new TextBox() { Name = "placeholder", Margin = default_space },
                        width = new TextBox() { Name = "width", Margin = default_space },
                        control_blank = new TextBox() { Name = "blank_alert", Margin = default_space };


                    MaterialDesignThemes.Wpf.HintAssist.SetHint(placeholder, "Placeholder (Optional)");
                    MaterialDesignThemes.Wpf.HintAssist.SetHint(width, "Control Width (number only)");
                    MaterialDesignThemes.Wpf.HintAssist.SetHint(control_blank, "No Value Warning (Blank)");
                    
                    content.Children.Add(placeholder);
                    content.Children.Add(width);
                    content.Children.Add(blank);
                    content.Children.Add(control_blank);

                    if(t == 't')
                    {
                        TextBox controlFile = new TextBox() { Name = "write_to", Margin = default_space };

                        MaterialDesignThemes.Wpf.HintAssist.SetHint(controlFile, "Write file (Optional)");

                        content.Children.Add(controlFile);

                        controlFile = null;
                    }

                    placeholder = null;
                    width = null;
                    blank = null;
                    control_blank = null;
                }

                if(t == 'c')
                {
                    // If the control type is a check box
                    TextBox valTrue = new TextBox() { Name = "return_true", Margin = default_space },
                        valFalse = new TextBox() { Name = "return_false", Margin = default_space };

                    MaterialDesignThemes.Wpf.HintAssist.SetHint(valTrue, "On True/Checked");
                    MaterialDesignThemes.Wpf.HintAssist.SetHint(valFalse, "On False/Unchecked");

                    // TODO: Validate if at least one has a value

                    content.Children.Add(valTrue);
                    content.Children.Add(valFalse);

                    valTrue = null;
                    valFalse = null;
                }

                if(t == 'l')
                {
                    // If the control type is a combo box 

                    //TODO: DO an operation with "combo_target" to "combo-target" once validating (Name cannot contain "-")
                    TextBox comboTar = new TextBox { Name = "combo_target", Margin = default_space };

                    Button keyPair = new Button { Content = "Set Key Pairs" };

                    keyPair.Click += ComboStrictDialog;

                    keyPair.Unloaded += (_, e) =>
                    {
                        keyPair.Click -= ComboStrictDialog;
                        e = null; _ = null;
                    };

                    MaterialDesignThemes.Wpf.HintAssist.SetHint(comboTar, "File/Folder Search (\\<file>;*.<ext>)");

                    content.Children.Add(comboTar);
                    content.Children.Add(keyPair);
                    comboTar = null;
                    keyPair = null;
                }
            }

            content = null;
            GC.Collect();
        }
        #endregion

        #region Control Functions/Methods
        
        private void GenerateJSON()
        {
            /*if (output == null)
            {
                output = new JObject();
            }

            JObject controlOut = new JObject();

            ctrl_type = null;

            int ctrl_idx = ControlType.SelectedIndex;
            controlOut.Add("type", new JValue(
                ctrl_idx == 0 ? "input" :
                ctrl_idx == 1 ? "pass" :
                ctrl_idx == 2 ? "check" :
                "combo"
            ));

            content = null;

            if (!output.ContainsKey(cate))
            {
                output.Add(cate, new JObject());
            }

            JObject why = output.SelectToken(cate) as JObject;
            why?.Add(ControlName.Text, controlOut.Root);*/

        }

        private bool IterateControls(ref StackPanel content, ref List<string> control, out string fail_reason)
        {
            int len = content.Children.Count;
          
            Type ctrl_type = null;
            TextBox ctrl_tb_placeholder = null;
            ComboBox ctrl_cb_placeholder = null;
            CheckBox ctrl_chb_placeholder = null;

            try
            {
                for (int i = 0; i < len; i++)
                {
                    // Get the control type of the control (TextBox or ComboBox etc) 
                    ctrl_type = content.Children[i].GetType();

                    if (Equals(ctrl_type, typeof(TextBox)))
                    {
                        ctrl_tb_placeholder = (TextBox)content.Children[i];

                        bool hasLength = ctrl_tb_placeholder.Text.Length > 0;

                        if ((int?)ctrl_tb_placeholder?.Tag == 1 && !hasLength)
                        {
                            fail_reason = $"A text field '{ctrl_tb_placeholder?.Name}' was left blank, where its required to set a value!";
                            return false;
                        }

                        if (hasLength)
                            control.Add($"{ctrl_tb_placeholder?.Name}={ctrl_tb_placeholder.Text}");

                        ctrl_tb_placeholder = null;
                    }
                    else if (Equals(ctrl_type, typeof(ComboBox)))
                    {
                        ctrl_cb_placeholder = (ComboBox)content.Children[i];

                        bool hasLength = ctrl_cb_placeholder.Text.Length > 0;

                        if ((int?)ctrl_cb_placeholder?.Tag == 1 && !hasLength)
                        {
                            fail_reason = $"A combo field '{ctrl_cb_placeholder?.Name}' was left blank, where its required to set a value!";
                            return false;
                        }

                        if (ctrl_cb_placeholder.Text.Length > 0)
                            control.Add($"{ctrl_cb_placeholder?.Name}={ctrl_cb_placeholder.Text}");

                        ctrl_cb_placeholder = null;
                    }
                    else
                    {
                        ctrl_chb_placeholder = (CheckBox)content.Children[i];

                        control.Add($"{ctrl_chb_placeholder?.Name}={ctrl_chb_placeholder.IsChecked}");

                        ctrl_chb_placeholder = null;
                    }
                }

                // Final Error Checks

                // Check 1: If set blank is true, an alert message should be set with it
                if (control.Contains("can_leave_blank=False"))
                {
                    string target_ba = "blank_alert";
                    if (!control.Any(x => x.StartsWith(target_ba)))
                    {
                        target_ba = null;
                        fail_reason = "Since 'Can Leave Blank' is false, you need to provide a message if its left blank\n(requires 'blank_alert' attribute / \"No Value Warning\" text box )";
                        return false;
                    }
                }
            }
            finally
            {
                ctrl_type = null;
                ctrl_tb_placeholder = null;
                ctrl_cb_placeholder = null;
                ctrl_chb_placeholder = null;
            }

            fail_reason = string.Empty;
            return true;
        }

        private void AddComponent_Click(object sender, RoutedEventArgs e)
        {
            // ADD TO CONFIG button
            sender = null;
            e = null;

            if (priorityControlLevel <= 1)
            {
                dh.OKDialog(
                    priorityControlLevel == 0 ? "A control must have a name" :
                    "A control must have a type and belong to at least a category."
                );
                return;
            }

            // We validate if a control exists already

            // Step 1: Validate if a category exists...
            if(output_directory.Value.ContainsKey(ControlCategory.Text))
            {
                // Step 2: We now check if a control of this name exists
                if(output_directory.Value[ControlCategory.Text].ContainsKey(ControlName.Text))
                {
                    // We then complain about it to the user...
                    dh.OKDialog($"A control of this name already exists as \"{ControlName.Text}\" under category: \"{ControlCategory.Text}\"\nPlease change the name to prevent duplicates!");
                    return;
                }
            }

            StackPanel content = ((ScrollViewer)ControlExtra.Content).Content as StackPanel;

            int len = content.Children.Count;

            int ctrl_idx = ControlType.SelectedIndex;

            // Add the current object
            List<string> control = new List<string>(len);

            bool fault = IterateControls(ref content, ref control, out string fault_reason);

            if (!fault)
            { 
                string cate = ControlCategory.Text;

                if (!output_directory.Value.ContainsKey(cate))
                {
                    int loopLen = control.Count;
                    
                    output_directory.Value.Add(cate, new Dictionary<string, string[]>(loopLen));

                    string propName = ControlName.Text;

                    output_directory.Value[cate].Add(propName, new string[loopLen]);

                    for (int i = 0; i < loopLen; i++)
                    {
                        output_directory.Value[cate][propName][i] = control[i];
                    }

                    propName = null;
                }

                cate = null;
                
                // Reset the controls for the next one
                ControlName.Text = null;
                ControlType.SelectedItem = -1;
                // ControlCategory.SelectedIndex = -1; ~ Category won't be changed as this will make the UI experience annoying
            } 
            else
            {
                dh.OKDialog(fault_reason);
            }

            control = null;
            content = null;
            fault_reason = null;
        }
        #endregion

        #region Window Events
        private void Destory()
        {
            if(!disposed)
            {
                dh.Destory();
                dh = null;

                ControlName.TextChanged -= ControlName_TextChanged;
                ControlType.SelectionChanged -= ControlType_SelectionChanged;
                ControlCategory.SelectionChanged -= ControlCategory_SelectionChanged;

                disposed = true;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            dh = new UIComponents.DialogHostContent(RootDialog, true, true);
            
            table = new Lazy<DataTable>();
            output_directory = new Lazy<Dictionary<string, Dictionary<string, string[]>>>();

            PriorityChanged();

            ControlName.TextChanged += ControlName_TextChanged;
            ControlType.SelectionChanged += ControlType_SelectionChanged;
            ControlCategory.SelectionChanged += ControlCategory_SelectionChanged;

            sender = null;
            e = null;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Destory();

            App.CancelClose = true;
            App.WindowClosed(this);
            App.WindowOpen(new main_view());

            sender = null;
            e = null;
        }
        #endregion

    }
}
