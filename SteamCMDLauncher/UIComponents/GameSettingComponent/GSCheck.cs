using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;

namespace SteamCMDLauncher.UIComponents.GameSettingComponent
{
    class GSCheck : ISettingConstruct
    {
        CheckBox cb;

        public GameSettingControl self { get; set; }
        private string[] valueReturn;

        // This component will not likely return true or false due to its nature
        public bool IsEmpty { get { return false; } }

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

        public string GetParam()
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
    }
}
