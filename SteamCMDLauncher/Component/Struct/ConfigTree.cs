using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace SteamCMDLauncher.Component.Struct
{
    public class ConfigTree
    {
        public string Name { get; set; }

        public ObservableCollection<ConfigTreeItem> Controls { get; set; }

        public ConfigTree()
        {
            Controls = new ObservableCollection<ConfigTreeItem>();
        }

        public void Add(string name, string type)
        {
            Controls.Add(new ConfigTreeItem(name, type) { Parent = Name });
            name = null; type = null;
        }

        public ConfigTree CopyClear()
        {
            ConfigTree self = new ConfigTree();
            
            self.Name = this.Name;
            self.Controls = this.Controls;
            this.Name = null;
            this.Controls = null;

            return self;
        }
    }

    public class ConfigTreeItem
    {
        public string CName { get; }
        public string Icon { get; private set; }
        public string Parent;

        public ConfigTreeItem(string Name, string Type)
        {
            CName = Name;
            switch (Type)
            {
                case "input": Icon = "FormatText"; break;
                case "pass":  Icon = "FormTextboxPassword"; break;
                case "combo": Icon = "FormDropdown"; break;
                case "check": Icon = "CheckboxMultipleMarkedOutline"; break;
                default: throw new Exception($"[CFG-G] Unknown Binding PackIcon was called ({Type})");
            }
            Name = null;
            Type = null;
        }

        /// <summary>
        /// Returns back the JSON type of the control
        /// </summary>
        public string GetActualType => Icon == "FormatText" ? "input" :
            Icon == "FormTextboxPassword" ? "pass" : Icon == "FormDropdown" ? "combo" :
            Icon == "CheckboxMultipleMarkedOutline" ? "combo" : string.Empty;
    }
}
