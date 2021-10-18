#define SKIP_JSON_LOAD
#define FORCE_LOAD

using Newtonsoft.Json.Linq;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.IO;
using System.ComponentModel;

namespace SteamCMDLauncher.Views
{
    /// <summary>
    /// Interaction logic for ConfigGen.xaml
    /// </summary>
    public partial class ConfigGen : Window, INotifyPropertyChanged
    {
        #region Attributes
        private int priorityControlLevel = 0;
        private bool disposed = false;

        private bool isLoadedConfig = false;
       
        private char lastControlType = '\0';
        #endregion

        #region Class Attributes
        
        private UIComponents.DialogHostContent dh;
        
        private Lazy<DataTable> table;
        private List<FrameworkElement> CurrentControls;

        private Lazy<Dictionary<string, string>> preloaded_Language;
        private Lazy<Dictionary<string, Dictionary<string, string[]>>> output_directory;
        
        // TODO: Does this variable need to be for loading a config?
        // private Lazy<Dictionary<string, string>> output_lang_directory;
        
        private Lazy<JObject> output;
        private Lazy<JObject> lang_output;
        #endregion

        #region String Attributes/Binding
        private string target = string.Empty;
        private string preCommands = string.Empty;
        private string joinCommand = string.Empty;

        public string Target { get => target; set { target = value; OnPropertyChanged(nameof(Target)); } }
        public string PreComands { get => preCommands; set { preCommands = value; OnPropertyChanged(nameof(PreComands)); } }
        public string JoinCommand { get => joinCommand; set { joinCommand = value; OnPropertyChanged(nameof(JoinCommand)); } }

        public ObservableCollection<Component.Struct.ConfigTree> TreeModel { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventArgs propChanged = new PropertyChangedEventArgs(propertyName);
            
            PropertyChanged?.Invoke(this, propChanged);
            
            propertyName = null;
            
            propChanged = null;
        }
        #endregion

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
            // Stop right away if the cancel prompt is used ( cancel button -> /0 )
            if (input == "/0") return;

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
            input = null;
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

            // TODO: This crashes if you type a letter - resolve?

            // This may be resolved by using 'as' instead of boxing as its type-safe
            if (!(ControlCategory.Items.GetItemAt(currentIndex) is ComboBoxItem currentCombo))
            {
                Config.Log("[CFG-G] currentCombo is null - this means a letter may have been typed in or is an error");
                return;
            }

            // Old expression: Equals(currentCombo.Tag, null) ? false : currentCombo.Tag.Equals(tag_look_for)
            bool AddNewCategory = !Equals(currentCombo.Tag, null) && currentCombo.Tag.Equals(tag_look_for);
            
            tag_look_for = null;

            if (AddNewCategory)
            {
                dh.InputDialog("Add New Category", "Please name the new category... Only Letters are allowed", NewCategoryAdd);
                ControlCategory.SelectedItem = null;
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

        #region Binding Related/Controlled
        private void GenerateTree()
        {
            // Clear is used/contains anything
            if (TreeModel.Count > 0) TreeModel.Clear();

            Component.Struct.ConfigTree config = null;
            string type = string.Empty;

            // Loop through each category then values
            foreach (KeyValuePair<string, Dictionary<string, string[]>> category in output_directory.Value)
            {
                config = new Component.Struct.ConfigTree() { Name = category.Key };

                foreach (string ctrl in category.Value.Keys)
                {
                    type = category.Value[ctrl].SingleOrDefault(x => x.StartsWith("type="));

                    if(type is null)
                    {
                        // Check if its validate-folder
                        if(!ctrl.Equals("#validate"))
                        {
                            throw new Exception($"[CFG-G] Control named '{ctrl}' didn't return back a control type\n(Missing 'type=' flag)");            
                        }

                        // Do validate-folder here

                        // "#validate"
                        config.ValidateTab = string.Join(';', category.Value[ctrl]);

                        category.Value.Remove("#validate");

                        continue;
                    }

                    config.Add(ctrl, type[5..]);
                }

                TreeModel.Add(config.CopyClear());
            }
            type = null;
            config = null;
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

            if (table is null) table = new Lazy<DataTable>();

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
            TextBlock No_content = new TextBlock { Text = "No Type Was Selected" };

            if(CurrentControls is null)
            {
                CurrentControls = new List<FrameworkElement>(10);
            }

            if (clear)
            {
                content.Children.Clear();
                content.Children.Add(No_content);
                lastControlType = '\0';
            }
            else
            {
                // Text, Password, Check, Combo (l)
                char[] compoent_idx = new char[] { 't', 'p', 'c', 'l' };
                
                char t = compoent_idx[ControlType.SelectedIndex];

                // Clear the controls ONLY if the control has been changed
                if (lastControlType == '\0')
                {
                    // If any text block that says "No type" exists
                    if (content.Children.Count == 1 && content.Children[0] is TextBlock)
                    {
                        content.Children.Clear();
                    }
                   
                    lastControlType = t;
                }
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
                    No_content = null;
                    return;
                }

                CurrentControls.Clear();

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

                // Add control references
                CurrentControls.Add(control_type);
                CurrentControls.Add(control_label);
                CurrentControls.Add(control_cmd);
                CurrentControls.Add(control_cmd_pf);
                CurrentControls.Add(control_tag);
                CurrentControls.Add(control_def);
                CurrentControls.Add(control_hint);
                CurrentControls.Add(control_alert);

                /*control_type = null;
                control_label = null;
                control_def = null;
                control_hint = null;
                control_alert = null;
                control_cmd = null;
                control_cmd_pf = null;
                control_tag = null;*/

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

                    CurrentControls.Add(placeholder);
                    CurrentControls.Add(width);
                    CurrentControls.Add(blank);
                    CurrentControls.Add(control_blank);

                    if (t == 't')
                    {
                        TextBox controlFile = new TextBox() { Name = "write_to", Margin = default_space };

                        MaterialDesignThemes.Wpf.HintAssist.SetHint(controlFile, "Write file (Optional)");

                        content.Children.Add(controlFile);
                        CurrentControls.Add(controlFile);

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

                    CurrentControls.Add(valTrue);
                    CurrentControls.Add(valFalse);

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
                        Button self = (Button)_;

                        if (keyPair != null)
                            keyPair.Click -= ComboStrictDialog;
                        
                        self.Click -= ComboStrictDialog;

                        e = null; _ = null; self = null;
                    };

                    MaterialDesignThemes.Wpf.HintAssist.SetHint(comboTar, "File/Folder Search (\\<file>;*.<ext>)");

                    content.Children.Add(comboTar);
                    content.Children.Add(keyPair);

                    CurrentControls.Add(comboTar);
                    CurrentControls.Add(keyPair);
                    
                    comboTar = null;
                    keyPair = null;
                }
            }

            content = null;
            No_content = null;
            GC.Collect();
        }
        #endregion

        #region Control Functions/Methods
        
        private void GenerateJSON(string filename, bool languageSupport)
        {
            if (output is null)
            {
                output = new Lazy<JObject>();
            }

            if (lang_output is null && languageSupport)
            {
                lang_output = new Lazy<JObject>();
            }

            JObject controlOut = null;

            // Lets now iterate over each control
            JObject why = null;
            int lenSize = 0;
            string[] valSplit = new string[2];
            
            // Null-these!
            string cKey = string.Empty;
            string cTranslations = string.Empty;
            string[] TextTranslationTypes = new string[] { "text", "placeholder", "alert", "hint", "blank_alert" };
            string translation_name = string.Empty;

            // Deal with the name first
            //TODO: Handle saving localization file

            // Lets deal with the setup key group first (if any)

            if(!string.IsNullOrEmpty(Target) || !string.IsNullOrEmpty(PreComands) || !string.IsNullOrEmpty(JoinCommand))
            {
                // Lets now add the setup key
                JObject setupNode = new JObject();

                if (!string.IsNullOrEmpty(Target)) setupNode.Add("target", Target);
                if (!string.IsNullOrEmpty(PreComands)) setupNode.Add("precommands", PreComands);
                if (!string.IsNullOrEmpty(JoinCommand)) setupNode.Add("server_join_command", JoinCommand);

                output.Value.Add("setup", setupNode);

                setupNode = null;
            }
            
            foreach (KeyValuePair<string, Dictionary<string, string[]>> cate in output_directory.Value)
            {
                cKey = !languageSupport ? cate.Key : $"#{cate.Key}";

                // Create the directory key if it hasn't already (likely not)
                if (!output.Value.ContainsKey(cKey))
                {
                    output.Value.Add(cKey, new JObject());

                    if (languageSupport)
                        lang_output.Value.Add(cKey, cKey[1..]);
                }

                // Now we iterate the controls onto it
                
                // We select the current iteration category as an Object
                why = output.Value.SelectToken(cKey) as JObject;
                
                // Then we loop over the values
                foreach (KeyValuePair<string, string[]> ctrl in cate.Value)
                {
                    // [NOTE] Key is the control name, Value is all the properties for that control

                    // Create a new JsonObject for the controls (node)
                    controlOut = new JObject();

                    // Get its fixed length size
                    lenSize = ctrl.Value.Length;
                    
                    // Loop through each element available
                    for (int i = 0; i < lenSize; i++)
                    {
                        // Get the contents by splitting at the first 'equals' occurrence
                        valSplit = ctrl.Value[i].Split('=', 2);
                        
                        // If its a table, we have to restructure it again!
                        if(valSplit[0] == "combo-strict")
                        {
                            JObject table = new JObject();

                            string data = valSplit[1][1..^1];

                            string[] row = data.Split(';')
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .ToArray();

                            string[] col = new string[2];
                            int rowLen = row.Length;
                            
                            for (int z = 0; z < rowLen; z++)
                            {
                                col = row[z].Split('|', 2);
                                table.Add(col[0], col[1]);
                            }

                            col = null;                         
                            data = null;
                            row = null;

                            // Then we add it the control
                            controlOut.Add(valSplit[0], table.Root);
                            table = null;
                            
                            continue;
                        }

                        if(languageSupport && TextTranslationTypes.Contains(valSplit[0]))
                        {
                            cTranslations = $"#{cate.Key}_{valSplit[0]}";

                            // Add the type ([0]) and its value ([1])
                            controlOut.Add(valSplit[0], cTranslations);

                            lang_output.Value.Add(cTranslations, valSplit[1]);

                            cTranslations = null;
                        }
                        else
                        {
                            // Add the type ([0]) and its value ([1])
                            controlOut.Add(valSplit[0], valSplit[1]);
                        }
                    }

                    // Then add it to the root, we're done here for the category - move to the next category available
                    why.Add(ctrl.Key, controlOut.Root);
                }
            }

            valSplit = null;
            why = null;
            controlOut = null;
            filename = null;
            cKey = null;
            cTranslations = null;
            TextTranslationTypes = null;
        }

        private bool IterateControls(ref StackPanel content, ref List<string> control, out string fail_reason)
        {
            int len = content.Children.Count;
          
            Type ctrl_type = null;
            TextBox ctrl_tb_placeholder = null;
            ComboBox ctrl_cb_placeholder = null;
            CheckBox ctrl_chb_placeholder = null;
            StringBuilder sb = new StringBuilder();

            bool to_add = false;

            try
            {
                for (int i = 0; i < len; i++)
                {
                    to_add = false;

                    // Get the control type of the control (TextBox or ComboBox etc) 
                    ctrl_type = content.Children[i].GetType();

                    if (Equals(ctrl_type, typeof(TextBox)))
                    {
                        ctrl_tb_placeholder = (TextBox)content.Children[i];

                        to_add = ctrl_tb_placeholder.Text.Length > 0;

                        if ((int?)ctrl_tb_placeholder?.Tag == 1 && !to_add)
                        {
                            fail_reason = $"A text field '{ctrl_tb_placeholder?.Name}' was left blank, where its required to set a value!";
                            return false;
                        }

                        sb.AppendFormat("{0}={1}", ctrl_tb_placeholder?.Name, ctrl_tb_placeholder.Text);

                        ctrl_tb_placeholder = null;
                    }
                    else if (Equals(ctrl_type, typeof(ComboBox)))
                    {
                        ctrl_cb_placeholder = (ComboBox)content.Children[i];

                        to_add = ctrl_cb_placeholder.Text.Length > 0;

                        if ((int?)ctrl_cb_placeholder?.Tag == 1 && !to_add)
                        {
                            fail_reason = $"A combo field '{ctrl_cb_placeholder?.Name}' was left blank, where its required to set a value!";
                            return false;
                        }
                        
                        sb.AppendFormat("{0}={1}", ctrl_cb_placeholder?.Name, ctrl_cb_placeholder.Text);

                        ctrl_cb_placeholder = null;
                    }
                    else if (Equals(ctrl_type, typeof(CheckBox)))
                    {
                        ctrl_chb_placeholder = (CheckBox)content.Children[i];

                        sb.AppendFormat("{0}={1}", ctrl_chb_placeholder?.Name, ctrl_chb_placeholder.IsChecked);

                        ctrl_chb_placeholder = null;
                    }

                    if(to_add) control.Add(sb.ToString());
                    
                    sb.Clear();
                }

                // Initialize DataTable if set (Prop name is: combo-strict)
                if (table.IsValueCreated)
                {
                    sb.Clear();
                    int rowCount = table.Value.Rows.Count;
                    object[] row = null;

                    sb.Append("combo-strict=[");

                    for (int i = 0; i < rowCount; i++)
                    {                      
                        row = table.Value.Rows[i].ItemArray;
                        
                        sb.Append(string.Join('|', row));

                        if (i < rowCount) sb.Append(';');
                    }
                    
                    sb.Append("]");
                    control.Add(sb.ToString());
                    
                    sb.Clear();
                    row = null;
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
                sb = null;
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

            bool no_fault = IterateControls(ref content, ref control, out string fault_reason);

            if (no_fault)
            {
                string cate = ControlCategory.Text;

                int loopLen = control.Count;

                if (!output_directory.Value.ContainsKey(cate))
                {
                    output_directory.Value.Add(cate, new Dictionary<string, string[]>(loopLen));
                }
                
                string propName = ControlName.Text;

                output_directory.Value[cate].Add(propName, new string[loopLen]);

                for (int i = 0; i < loopLen; i++)
                {
                    output_directory.Value[cate][propName][i] = control[i];
                }

                propName = null;

                cate = null;
                
                // Reset the controls for the next one
                ControlName.Text = null;
                ControlType.SelectedItem = -1;
                // ControlCategory.SelectedIndex = -1; ~ Category won't be changed as this will make the UI experience annoying

                // Reset table for next control
                table = null;
                
                GenerateTree();
            } 
            else
            {
                dh.OKDialog(fault_reason);
            }

            control = null;
            content = null;
            fault_reason = null;
        }
        
        private void SafeConfig_Click(object sender, RoutedEventArgs e)
        {
            // Save Config Button
            if(!output_directory.IsValueCreated || output_directory.Value.Count < 1)
            {
                dh.OKDialog("No Controls have been inserted yet... please do so before saving!");
                return;
            }

            string fileName = string.Empty;

            bool includeLanguageSupport = false;

            dh.YesNoDialog("Multiple Language Support", "Does your config support multiple languages?\n(Click 'No' if its intended for personal usage!)", new Action(() => includeLanguageSupport = true));

            dh.InputDialog("Save Config",
                "What do you want to name your configuration? (Just the filename, not with extension!)\nNote: You'll need to find the AppID in order for the program to detect it.",
                new Action<string>((x) => fileName = x));

            // Check if the fileName variable contains any characters that are not allowed in a file name
            char[] ill = System.IO.Path.GetInvalidFileNameChars();
            if (fileName.Any(x => ill.Contains(x)))
            {
                dh.OKDialog($"The given filename '{fileName}' contained illegal characters, please ensure you don't have any!");
                ill = null;
                fileName = null;
                return;
            }

            GenerateJSON(fileName, includeLanguageSupport);

            // Output the file itself
            string output_location = System.IO.Path.Combine(Environment.CurrentDirectory, "Resources", $"{fileName}.json");
            string lang_output_location = includeLanguageSupport ? System.IO.Path.Combine(Environment.CurrentDirectory, "Resources", $"{fileName}_en.json") : string.Empty;

            System.IO.File.WriteAllText(output_location, output.Value.ToString());
            
            if(!string.IsNullOrEmpty(lang_output_location))
            {
                System.IO.File.WriteAllText(lang_output_location, lang_output.Value.ToString());
            }

            if(System.IO.File.Exists(output_location))
            {
                dh.YesNoDialog("Success", "Your file has been saved in the configuration folder!\nWould you like to view it now?",
                new Action(() => { System.Diagnostics.Process.Start("explorer.exe", output_location); }));
            } else
            {
                dh.OKDialog("The file creation process has failed, check the log file if an exception wasn't thrown.");
            }

            output = null;
            lang_output = null;

            ill = null;
            fileName = null;
            output_location = null;
        }
        
        private void LoadTokens(JToken[] elements)
        {
            int iterationLen = elements.Length;
            bool language_file = preloaded_Language.IsValueCreated || preloaded_Language.Value != null;

            // Reset the objects to read the new files

            CurrentControls = null;
            table = null;
            output_directory = new Lazy<Dictionary<string, Dictionary<string, string[]>>>();

            string key_iter = null;
            string ctrl_name = null;
            string ctrl_value = null;
            JProperty[] iter = null, inner_iter = null;

            try
            {
                for (int i = 0; i < iterationLen; i++)
                {
                    // Get the category name
                    key_iter = language_file ? preloaded_Language.Value[((JProperty)elements[i]).Name] : ((JProperty)elements[i]).Name;

                    // Create the category with 5 capacity by default
                    output_directory.Value.Add(key_iter, new Dictionary<string, string[]>(5));

                    // Lets now load each one in
                    iter = elements[i].Children<JObject>()
                        .Properties()
                        .ToArray();

                    inner_iter = null;

                    int iterSize = iter.Length;
                    int innerIterSize = 0;

                    for (int j = 0; j < iterSize; j++)
                    {
                        // Get the controls over-all name
                        ctrl_name = iter[j].Name;

                        // If its to enable an tab
                        if(ctrl_name == "validate-folder")
                        {
                            string val = iter[j]
                                .Value.
                                ToString()[1..^1]
                                .Replace("\r\n", string.Empty)
                                .Trim();

                            output_directory.Value[key_iter].Add("#validate", val.Split(", "));
                            
                            val = null;
                            
                            continue;
                        }

                        // Add it to the dictionary with a size of arguments available
                        inner_iter = iter[j].Children<JObject>().Properties().ToArray();
                    
                        innerIterSize = inner_iter.Length;
                    
                        output_directory.Value[key_iter].Add(ctrl_name, new string[innerIterSize]);

                        for (int z = 0; z < innerIterSize; z++)
                        {
                            ctrl_value = inner_iter[z].Value.ToString();

                            if (ctrl_value[0] == '#')
                                ctrl_value = preloaded_Language.Value[ctrl_value];

                            output_directory.Value[key_iter][ctrl_name][z] = $"{inner_iter[z].Name}={ctrl_value}";
                        }
                    }

                    iter = null;
                    inner_iter = null;
                }
            }
            catch (Exception)
            {
                Config.Log("[CFG-G] LoadTokens function throw an error - related to the json file or logic!");
                throw;
            }
            finally
            {
                iter = null;
                inner_iter = null;
                elements = null;
                key_iter = null;
                ctrl_name = null;
                ctrl_value = null;
            }
        }

        private void LoadConfig_Click(object sender, RoutedEventArgs e)
        {
            // Load config button
            bool requires_localize_file = false;

            if(!isLoadedConfig) isLoadedConfig = true;

            string load_file = null;
            string[] availableFiles = null;

            JObject parseFile = null;
            JObject parseFile_lang = null;

            System.Threading.Tasks.ValueTask<bool> isFile;
            
            preloaded_Language = null;
            
            try
            {
                Config.Log("[CFG-G] Started looking for Config and Language Config (If any)");

                // Get the file the load in

#if !SKIP_JSON_LOAD || !DEBUG
                load_file = Config.GetFile(".json", "Resources");
#else
                // Skip loading process by pre-loading a config
                load_file = "C:\\Users\\james\\source\\repos\\SteamCMDLauncher\\SteamCMDLauncher\\bin\\Debug\\netcoreapp3.1\\Resources\\game_setting_740.json";
#endif
                // Validate if a file has been chosen
                if (load_file.Length <= 1) return;

                if(!File.Exists(load_file))
                {
                    dh.OKDialog("Configuration failed as it didn't exist.");
                    return;
                }

                // Parse the file as an JObject
                parseFile = JObject.Parse(File.ReadAllText(load_file));

                // Validate if we need localize file...
                foreach (JProperty item in parseFile.Children<JProperty>())
                {
                    if (item.Name.StartsWith("#")) { requires_localize_file = true; break; }
                }

                // Get the current file that is loaded in's folder location and exact name (without extension)
                string currentFolder = Path.GetDirectoryName(load_file);
                string currentFile = Path.GetFileNameWithoutExtension(load_file);
                
                // Create an Enumerable to hold all language files
                availableFiles = Directory.GetFiles(currentFolder)
                    .Where(x => x.Contains($"{currentFile}_"))
                    .ToArray();

                currentFolder = null; currentFile = null;

                // Check if any files found, report if not
                if(requires_localize_file && availableFiles.Count() == 0)
                {
                    Config.Log("[CFG-G] Required Localize File is set true, but no localize files were found! (_*.json)");
                    dh.OKDialog("No Localize File Found - it is required that an English Translation is available by default!");
                    return;
                }
                else
                {
                    Config.Log("[CFG-G] Localize File was found, now perform logic to cache it.");
                    
                    bool required_file = false;
                    int len = availableFiles.Length;
                    string required_file_loc = string.Empty;
                    
                    if(len == 1)
                    {
                        required_file_loc = availableFiles[0];
                        required_file = true;
                    }
                    else
                    {
                        string shortname = null;
                        
                        for (int i = 0; i < len; i++)
                        {
                            // Get the filename only of the file
                            shortname = Path.GetFileNameWithoutExtension(availableFiles[i]);
                            
                            // Ask if the file is the correct one
                            isFile = dh.YesNoDialog($"File {i + 1}/{len}", $"Is the file {shortname}?");

                            // If so, stop and note it
                            if(isFile.Result)
                            {
                                required_file_loc = availableFiles[i];
                                required_file = true;
                                break;
                            }
                        }

                        shortname = null;

                    }

                    if(!string.IsNullOrEmpty(required_file_loc))
                    {
                        parseFile_lang = JObject.Parse(File.ReadAllText(required_file_loc));

                        preloaded_Language = new Lazy<Dictionary<string, string>>();

                        // Time to cache the variables

                        foreach (var pair in parseFile_lang.Properties())
                        {
                            preloaded_Language.Value.Add(pair.Name, pair.Value.ToString());
                        }

                        // Then unload the JObject
                        parseFile_lang = null;
                    }

                    required_file_loc = null;
                    
                    if(!required_file)
                    {
                        dh.OKDialog("No suitable localize file was selected - cannot continue until one is chosen.");
                        return;
                    }
                }

                Config.Log("[CFG-G] Starting parsing process");

                // Lets start with the setup arguments, as that doesn't require any form of iteration
                string setup_key = "setup";

                if(parseFile.ContainsKey(setup_key))
                {
                    Target = (string)(parseFile[setup_key]["target"] ?? string.Empty);
                    PreComands = (string)(parseFile[setup_key]["precommands"] ?? string.Empty);
                    JoinCommand = (string)(parseFile[setup_key]["server_join_command"] ?? string.Empty);
                }

                setup_key = null;

                // Now lets load in the controls (Skipping the setup key, which should ALWAYS be first)
                Config.Log("[CFG-G] Running LoadTokens");
                
                LoadTokens(parseFile.Children().Skip(1).ToArray());
                
                Config.Log("[CFG-G] Finished LoadTokens - Now loading GenerateTree");
                GenerateTree();
               
                Config.Log("[CFG-G] Loaded Config and Language Config (If any)");
            }
            catch (Exception)
            {
                //TODO: Make exceptions more helpful... maybe...
                Config.Log("[CFG-G] LoadConfig throw an error");
                throw;
            }
            finally
            {
                parseFile = null;
                load_file = null;
                parseFile_lang = null;
                availableFiles = null;
            }
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            //TODO: Turn this into 'Try Finally'?

            TreeView currentTree = (TreeView)sender;

            Component.Struct.ConfigTreeItem selectedItem = currentTree.SelectedItem as Component.Struct.ConfigTreeItem;

            // Returns null if the item is not a 'ConfigTreeItem'
            if (!(selectedItem is null))
            {

                string category = selectedItem.Parent;

                // Check if category exists
                if (!output_directory.IsValueCreated) return;

                if(output_directory.Value.ContainsKey(category))
                {
                    string[] value = null;
                                      
                    if (output_directory.Value[category].TryGetValue(selectedItem.CName, out value))
                    {
                        string[] split_array = new string[2];

                        int iter_size = value.Length;

                        // Set the control properties
                        ControlName.Text = selectedItem.CName;

                        int typeIdx = selectedItem.GetActualType switch
                        {
                            "input" => 0,
                            "pass" => 1,
                            "check" => 2,
                            "combo" => 3,
                            _ => -1
                        };

                        if (typeIdx == -1)
                        {
                            dh.OKDialog($"Unknown Type '{selectedItem.GetActualType}' was found, please correct this.");
                            return;
                        }
                        
                        ControlType.SelectedIndex = typeIdx;

                        int categoryIdx = -1;
                        
                        int cateCount = ControlCategory.Items.Count;

                        ComboBoxItem cbDummy = null;

                        for (int i = 0; i < cateCount; i++)
                        {
                            cbDummy = (ComboBoxItem)ControlCategory.Items[i];
                            if (cbDummy.Content.Equals(selectedItem.Parent))
                            {
                                categoryIdx = i;
                                cbDummy = null;
                                break;
                            }
                            cbDummy = null;
                        }

                        if (categoryIdx == -1)
                        {
                            dh.OKDialog($"Unknown Category '{selectedItem.Parent}' was found, please correct this.");
                            return;
                        }

                        ControlCategory.SelectedIndex = categoryIdx;
                        
                        if (CurrentControls is null)
                            throw new Exception("[CFG-G] CurrentControls variable in ConfigGen is empty - this should not be the case.");

                        FrameworkElement ctrl = null;

                        Type ctrl_t = null;


                        for (int i = 0; i < iter_size; i++)
                        {
                            split_array = value[i].Split('=', 2);

                            ctrl = CurrentControls.SingleOrDefault(x => x.Name == split_array[0]);

                            if(ctrl != null)
                            {
                                ctrl_t = ctrl.GetType();

                                if (ctrl_t.Equals(typeof(TextBox)))
                                   ((TextBox)ctrl).Text = split_array[1];
                                else if (ctrl_t.Equals(typeof(CheckBox)))
                                    ((CheckBox)ctrl).IsChecked = Convert.ToBoolean(split_array[1]);
                                else if (ctrl_t.Equals(typeof(ComboBox)))
                                    ((ComboBox)ctrl).SelectedItem = split_array[1];
                            }
                            else
                            {
                                if (split_array[0].StartsWith("combo-strict"))
                                {
                                    // Load the table data
                                    table = new Lazy<DataTable>();
                                    
                                    table.Value.Columns.Add("Key");
                                    table.Value.Columns.Add("Value");

                                    // Get the row data
                                    // Substring so that we eliminate the square brackets ( .Substring(1, len - 1) )
                                    ReadOnlySpan<char> SpanSlice = split_array[1][1..^1];

                                    string[] rows = SpanSlice.ToString().Split(";");
                                    string[] columns = null;

                                    int iterSize = rows.Length;
                                    for (int j = 0; j < iterSize; j++)
                                    {
                                        if (string.IsNullOrWhiteSpace(rows[j])) continue;

                                        columns = rows[j].Split('|');

                                        if (columns.Length > 2) throw new Exception($"[CFG-G] Row \"{rows[j]}\" contains more than 2 elements, this shouldn't be the case!");

                                        table.Value.Rows.Add(columns);
                                    }

                                    columns = null;
                                    SpanSlice = null;
                                    rows = null;
                                }
                            }
                        }
                        
                        ctrl_t = null;
                        ctrl = null;
                        split_array = null;
                        value = null;
                    }
                    else
                    {
                        dh.OKDialog($"Sorry but the control assigned to category {category} doesn't contain the control {selectedItem.CName}! This may be a fault!");
                    }
                }
                else
                {
                    dh.OKDialog($"Sorry but the category assigned to this control ({category}) doesn't exist or a fault has occurred!");
                }
             
                category = null;
            }

            e = null;
            selectedItem = null;
            sender = null;
            currentTree = null;
        
        }
        
        private void PackIcon_OnLeftClick(object sender, MouseButtonEventArgs e)
        {
            // Lock when Tab supports enable state
            MaterialDesignThemes.Wpf.PackIcon self = null;
            Component.Struct.ConfigTree tar = null;
            StringBuilder sb = null;
            
            string target = null;
            string[] target_val = null;

            try
            {
                // Get the sender and safe-cast it into an PackIcon
                self = sender as MaterialDesignThemes.Wpf.PackIcon;

                // Validate if its null - it means the object didn't cast into an PackIcon
                if (self is null) { dh.OKDialog("Problem: Memory or GC fault - Object you clicked on has lost reference in memory! Not good!"); return; }

                // Now get the information from Tag, safe-cast again to prevent errors
                target = self.Tag as string;

                // If tag is null, throw an error
                if (target is null) { dh.OKDialog("Problem: Section referencing was empty - this is a programming fault... Not good!"); return; }

                // Now lets try to get the clicked tab to fetch
                tar = TreeModel.SingleOrDefault(x => x.Name == target);
                
                // If the object is - somehow - not in memory or object any more, show error
                if(tar is null) { dh.OKDialog("Problem: Memory or GC fault - The target object is not kept in the global heap - Not good!"); return; }

                // Now get the lock state criteria's
                target = tar.ValidateTab;

                // If the target is not assigned or if the (null-safe) string's length is less than 1
                if(string.IsNullOrWhiteSpace(target) || target?.Length < 1)
                {
                    dh.OKDialog("Problem: The program shouldn't have flagged this tab with an lock state.\nIt may have been kept in memory from last config!");
                    return;
                }

                // This means all the information is available
                sb = new StringBuilder();
                sb.AppendLine("This tab is only enabled BASED on the following criteria:\n");

                // Split the single-line text into a string array based on character ';' (semi-colon)
                target_val = target.Split(';');

                // Get the initial array size, this won't likely change during iteration (cached)
                int tVal = target_val.Length;
                
                // A flag which states true or false depending if it contains a dot '.', which indicates its a file.
                bool isFolder = false;
                
                for (int i = 0; i < tVal; i++)
                {
                    // Set the state to reflect if its a folder or file
                    isFolder = !target_val[i].Contains('.');
                    
                    // Add the row number
                    sb.Append($"  {i + 1})  ");
                    
                    // Add the initial text which states its type
                    sb.Append(isFolder ? "Folder: " : "File: ");
                    
                    // Append the current assigned value for that row
                    sb.Append(target_val[i]);
                    
                    // Add an newline character, to indicate a new row
                    sb.Append('\n');
                }

                // Add the final initial warning / message
                sb.AppendLine("\nThese can only be changed in file to prevent issues long term!");

                // Show the buffer text to the dialog-host
                dh.OKDialog(sb.ToString());
            }
            finally
            {
                self = null;
                tar = null;
                sb = null;
                target = null;
                target_val = null;
                sender = null;
                e = null;
            }
        }
        
        //TODO: Implement Remove Tab?
        
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

                target = null;
                preCommands = null;
                joinCommand = null;

                table = null;
                output_directory = null;
                //output_lang_directory = null;
                output = null;
                lang_output = null;
                TreeModel = null;

                DataContext = null;
                
                disposed = true;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            dh = new UIComponents.DialogHostContent(RootDialog, true, true);
            
            table = new Lazy<DataTable>();
            output_directory = new Lazy<Dictionary<string, Dictionary<string, string[]>>>();
            //output_lang_directory = new Lazy<Dictionary<string, string>>();

            output = new Lazy<JObject>();
            lang_output = new Lazy<JObject>();

            PriorityChanged();

            ControlName.TextChanged += ControlName_TextChanged;
            ControlType.SelectionChanged += ControlType_SelectionChanged;
            ControlCategory.SelectionChanged += ControlCategory_SelectionChanged;

            // For the binding
            DataContext = this;

            TreeModel = new ObservableCollection<Component.Struct.ConfigTree>();

#if !DEBUG
            dh.OKDialog("This is in alpha stage - Save or Load at your own risk!");
#endif

#if FORCE_LOAD && DEBUG
            LoadConfig_Click(null, null);
#endif

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
