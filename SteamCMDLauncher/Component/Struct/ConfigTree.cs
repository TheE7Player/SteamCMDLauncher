using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace SteamCMDLauncher.Component.Struct
{
    public class ConfigTree
    {
        public string Name { get; set; }

        public string ValidateTab { get; set; }

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
            ConfigTree self = new ConfigTree
            {
                Name = Name,
                Controls = Controls,
                ValidateTab = ValidateTab
            };

            Name = null;
            Controls = null;
            ValidateTab = null;

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

            Icon = Type switch
            {
                "input" => Icon = "FormatText",
                "pass" => Icon = "FormTextboxPassword",
                "combo" => Icon = "FormDropdown",
                "check" => Icon = "CheckboxMultipleMarkedOutline",
                _ => throw new Exception($"[CFG-G] Unknown Binding PackIcon was called ({Type})")
            };

            Name = null;
            Type = null;
        }

        /// <summary>
        /// Returns back the JSON type of the control
        /// </summary>
        public string GetActualType => Icon switch
        {
            "FormatText" => "input",
            "FormTextboxPassword" => "pass",
            "FormDropdown" => "combo",
            "CheckboxMultipleMarkedOutline" => "check",
            _ => string.Empty
        };
    }
}
