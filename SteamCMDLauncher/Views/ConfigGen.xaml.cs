using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SteamCMDLauncher.Views
{
    /// <summary>
    /// Interaction logic for ConfigGen.xaml
    /// </summary>
    public partial class ConfigGen : Window
    {
        private int priorityControlLevel = 0;
        private UIComponents.DialogHostContent dh;
        private bool disposed = false;
        private Lazy<DataTable> table;

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

            content.Children.Clear();

            if (clear)
            {
                content.Children.Add(new TextBlock { Text = "No Type Was Selected" });
            }
            else
            {
                // Text, Password, Check, Combo (l)
                char[] compoent_idx = new char[] { 't', 'p', 'c', 'l' };
                char t = compoent_idx[ControlType.SelectedIndex];
                compoent_idx = null;

                Thickness default_space = new Thickness(0, 5, 0, 5);

                TextBox control_name = new TextBox() { Tag = "name", Margin = default_space };
                TextBox control_label = new TextBox() { Tag = "text", Margin = default_space };
                TextBox control_def = new TextBox() { Tag = "default", Margin = default_space };
                TextBox control_hint = new TextBox() { Tag = "hint", Margin = default_space };
                TextBox control_alert = new TextBox() { Tag = "alert", Margin = default_space };
                TextBox control_cmd = new TextBox() { Tag = "command", Margin = default_space };
                TextBox control_cmd_pf = new TextBox() { Tag = "command_prefix", Margin = default_space };
                
                ComboBox control_tag = new ComboBox() { Tag = "tag", Margin = default_space };

                // Add the tags that go alongside this control
                control_tag.Items.Add(new ComboBoxItem { Content = "NONE" });
                control_tag.Items.Add(new ComboBoxItem { Content = "IP" });
                control_tag.Items.Add(new ComboBoxItem { Content = "PASS" });
                control_tag.Items.Add(new ComboBoxItem { Content = "PORT" });

                // This entity is read only and is invisible (collapsed) and is auto-filled
                TextBox control_type = new TextBox()
                {
                    Tag = "type",
                    Margin = default_space,
                    IsEnabled = false,
                    Text = t == 't' ? "input" : t == 'p' ? "pass" : t == 'c' ? "check" : "combo",
                    Visibility = Visibility.Collapsed
                };

                MaterialDesignThemes.Wpf.HintAssist.SetHint(control_name, "Control Identifier (Not the name)");
                MaterialDesignThemes.Wpf.HintAssist.SetHint(control_label, "Control Label (Title)");
                MaterialDesignThemes.Wpf.HintAssist.SetHint(control_cmd, "Command (Denoted with $)");
                MaterialDesignThemes.Wpf.HintAssist.SetHint(control_cmd_pf, "Command Prefix");
                MaterialDesignThemes.Wpf.HintAssist.SetHint(control_def, "Default value (Optional)");
                MaterialDesignThemes.Wpf.HintAssist.SetHint(control_hint, "Hint (Optional)");
                MaterialDesignThemes.Wpf.HintAssist.SetHint(control_alert, "Alert Warning (Optional)");
                MaterialDesignThemes.Wpf.HintAssist.SetHint(control_tag, "Server Argument (Optional)");

                content.Children.Add(control_name);
                content.Children.Add(control_cmd);
                content.Children.Add(control_cmd_pf);
                content.Children.Add(control_type);
                content.Children.Add(control_label);
                content.Children.Add(control_tag);
                content.Children.Add(control_def);
                content.Children.Add(control_hint);
                content.Children.Add(control_alert);

                control_name = null;
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

                    CheckBox blank = new CheckBox() { Content = "Can Be Blank", Tag = "can_leave_blank", Margin = default_space };
                   
                    TextBox placeholder = new TextBox() { Tag = "placeholder", Margin = default_space },
                        width = new TextBox() { Tag = "width", Margin = default_space },
                        control_blank = new TextBox() { Tag = "blank_alert", Margin = default_space };


                    MaterialDesignThemes.Wpf.HintAssist.SetHint(placeholder, "Placeholder (Optional)");
                    MaterialDesignThemes.Wpf.HintAssist.SetHint(width, "Control Width (number only)");
                    MaterialDesignThemes.Wpf.HintAssist.SetHint(control_blank, "No Value Warning (Blank)");
                    
                    content.Children.Add(placeholder);
                    content.Children.Add(width);
                    content.Children.Add(blank);
                    content.Children.Add(control_blank);

                    if(t == 't')
                    {
                        TextBox controlFile = new TextBox() { Tag = "write_to", Margin = default_space };

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
                    TextBox valTrue = new TextBox() { Tag = "return_true", Margin = default_space },
                        valFalse = new TextBox() { Tag = "return_false", Margin = default_space };

                    MaterialDesignThemes.Wpf.HintAssist.SetHint(valTrue, "On True/Checked");
                    MaterialDesignThemes.Wpf.HintAssist.SetHint(valFalse, "On False/Unchecked");

                    content.Children.Add(valTrue);
                    content.Children.Add(valFalse);

                    valTrue = null;
                    valFalse = null;
                }

                if(t == 'l')
                {
                    // If the control type is a combo box 
                    TextBox comboTar = new TextBox { Tag = "combo-target", Margin = default_space };

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
        private void AddComponent_Click(object sender, RoutedEventArgs e)
        {
            // ADD TO CONFIG button
            sender = null;
            e = null;

            if(priorityControlLevel <= 1)
            {
                dh.OKDialog(
                    priorityControlLevel == 0 ? "A control must have a name" :
                    "A control must have a type and belong to at least a category."
                );
                return;
            }

            // Reset the controls for the next one
            ControlName.Text = null;
            ControlCategory.SelectedIndex = -1;
            ControlType.SelectedItem = -1;
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
