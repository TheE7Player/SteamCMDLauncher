using System.Windows.Controls;

namespace SteamCMDLauncher.UIComponents.GameSettingComponent
{
    class GSCheck : ISettingConstruct
    {
        CheckBox cb;

        public GameSettingControl self { get; set; }
        public string GetControlType => "gscheck";

        private string[] valueReturn;

        // This component will not likely return true or false due to its nature
        public bool IsEmpty { get { return false; } }

        public string SaveValue { get { return cb.IsChecked.ToString(); } }

        public GSCheck(GameSettingControl self, string[] return_value = null)
        {
            this.self = self;
            cb = new CheckBox();
            valueReturn = return_value;
        }

        public Control GetComponent()
        {
            if (!string.IsNullOrEmpty(self.defaultValue))
            {
                cb.IsChecked = self.defaultValue == "True";
            }

            return cb;
        }

        public void Discard()
        {
            self = null;
        }

        public string GetParam(string info = null)
        {
            if(valueReturn != null)
            {
                int state = (bool)cb.IsChecked ? 1 : 0;

                // Check if element is null (denoted with '_') - If so ignore it
                if (valueReturn[state] == "_") return string.Empty;

                return self.Command.Replace("$", valueReturn[state]);
            }

            return self.Command;
        }

        public void LoadValue(string value)
        {
            string r = value.ToLower().Trim();

            switch (r)
            {
                case "true": cb.IsChecked = true; break;
                case "false": cb.IsChecked = false; break;
                default: Config.Log($"[GSM] Cannot parse Checkbox '{r}' - Setting false by default");  cb.IsChecked = false; break;
            }
        }
    }
}
