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
    /// Interaction logic for ConfigGen.xaml
    /// </summary>
    public partial class ConfigGen : Window
    {
        private int priorityControlLevel = 0;
        private UIComponents.DialogHostContent dh;
        private bool disposed = false;

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
            ControlType.IsEnabled = priorityControlLevel == 1;
            ControlCategory.IsEnabled = priorityControlLevel == 1;

            ControlExtra.IsEnabled = priorityControlLevel == 2;
        }
        #endregion

        #region Control Events

        private bool AddingNewCategory = false;

        private void ControlCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            sender = null; e = null;

            if (AddingNewCategory) { AddingNewCategory = false; return; }

            string tag_look_for = "Add";
            int currentIndex = ControlCategory.SelectedIndex;

            bool AddNewCategory = ((ComboBoxItem)ControlCategory.Items.GetItemAt(currentIndex)).Tag.Equals(tag_look_for);
            tag_look_for = null;
                       
            if(AddNewCategory)
            {
                dh.InputDialog("Add New Category", "Please name the new category... Only Letters are allowed", new Action<string>((x) =>
                {
                    char[] letters = x.ToCharArray();

                    bool name_valid = true;

                    int letter_len = letters.Length;

                    if(letter_len < 2)
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

                    if(!name_valid)
                    {
                        dh.OKDialog($"The name \"{x}\" was invalid to use:\nThe key name must ONLY have letters - This means no numbers, symbols or spaces (use underscore for spacing)");
                    }
                    else
                    {
                        // Get the highest index possible ( n - 1 )
                        int old_len = ControlCategory.Items.Count - 1;
                        
                        // Get the last element - this is used to assign a new category
                        ComboBoxItem add_item = (ComboBoxItem)ControlCategory.Items.GetItemAt(old_len);
                        
                        // Create the new element to be added
                        ComboBoxItem new_item = new ComboBoxItem { Content = x };

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
                    }
                    
                    letters = null;
                }));
            }

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
