using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.IO;
using System.Linq;

namespace SteamCMDLauncher.UIComponents.GameSettingComponent
{
    class GSCombo : ISettingConstruct
    {
        private ComboBox cb;

        public GameSettingControl self { get; set; }

        public string GetControlType => "gscombo";

        public bool IsEmpty {
            get
            {
                if(cb is null) return true;

                return false;
            }
        }

        private bool strict_mode = false;

        private string dir_path;
        private string dir_parse;

        private string default_value;
        private string number_range;
        private Dictionary<string, string> strict_val;
        public string SaveValue { get { return cb.SelectedValue.ToString(); } }

        public void LoadValue(string value)
        {
            var obj = cb.Items.Cast<object>().FirstOrDefault(x => string.Compare(x.ToString(), value) == 0);

            if (obj is null)
                cb.SelectedIndex = 0;
            else
                cb.SelectedIndex = cb.Items.IndexOf(obj);
        }

        public GSCombo(GameSettingControl self)
        {
            this.self = self;
            cb = new ComboBox();
        }

        public void SetComboDir(string path) => this.dir_path = path;

        public void SetComboPattern(string pattern) => this.dir_parse = pattern;
       
        public void SetDefault(string value) => this.default_value = value;

        public void SetNumberRange(string range) => this.number_range = range;

        public void SetStrictValues(Dictionary<string, string> strict_values) 
        {
            strict_mode = true;
            strict_val = strict_values;
        }

        public Control GetComponent()
        {
            bool content_set = false;

            // Auto-size based on highest string len
            int max_length = 0;

            if(strict_mode)
            {
                foreach (var pair in strict_val)
                {
                    if (pair.Key.Length > max_length)
                        max_length = pair.Key.Length;

                    cb.Items.Add(pair.Key);
                }
            }
            else
            {
                if(!string.IsNullOrEmpty(dir_parse))
                {
                    //TODO: What if string array with multiple folders and file types?

                    string[] s_split = dir_parse.Split(';');

                    // For some reason Path.Combine doesn't concat the path safely somehow, using concat for now.
                    string folder = string.Concat(dir_path, s_split[0]);

                    var files = System.IO.Directory.GetFiles(folder, s_split[1]);

                    string filtered_name;

                    foreach (var map in files)
                    {
                        // Get the map name only
                        filtered_name = Path.GetFileNameWithoutExtension(map);

                        if (filtered_name.Length > max_length)
                            max_length = filtered_name.Length;

                        cb.Items.Add(filtered_name);
                    }

                    filtered_name = null;
                    content_set = true;
                }

                if(!string.IsNullOrEmpty(number_range) && !content_set)
                {
                    // TODO: Do validation if more ranges are set ( > 2 )
                    string[] range = number_range.Split("-");

                    int min = Convert.ToInt32(range[0]);
                    int max = Convert.ToInt32(range[1]);

                    for (int i = min; i <= max; i++)
                    {
                        cb.Items.Add(i);
                    }

                    content_set = true;
                }
            }

            cb.Width = max_length + 100;

            if (string.IsNullOrEmpty(self.defaultValue))
            { 
                cb.SelectedIndex = 0;
            } 
            else
            {
                // Try and find the default value
                var obj = cb.Items.Cast<object>().FirstOrDefault(x => string.Compare(x.ToString(), self.defaultValue) == 0);
                
                int idx = cb.Items.IndexOf(obj);
                
                cb.SelectedIndex = idx > -1 ? idx : 0;

                obj = null;
            }
                    
            return cb;
        }

        public void Discard()
        {
            self = null;
        }

        public string GetParam(string info = null)
        {
            // Validate if using strict mode
            if(!(strict_val is null))
            {
                if (strict_val.Count == 0)
                    throw new Exception("GSCombo strict values were not assigned in HEAP. Coding fault!");

                return self.Command.Replace("$", strict_val[cb.SelectedValue.ToString()]);
            }


            return self.Command.Replace("$", cb.SelectedValue.ToString());
        }
    }
}
