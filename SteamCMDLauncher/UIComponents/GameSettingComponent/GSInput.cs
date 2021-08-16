using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;

namespace SteamCMDLauncher.UIComponents.GameSettingComponent
{
    class GSInput : ISettingConstruct
    {
        private TextBox tb;

        public GameSettingControl self { get; set; }

        public bool IsEmpty
        {
            get
            {
                if (tb is null) return true;

                if (string.IsNullOrEmpty(tb.Text)) return true;

                if (tb.Text.Length < 1) return true;

                return false;
            }
        }

        public string SaveValue { get {

                if (IsEmpty) return null;

                return tb.Text.Trim();
            }
        }

        public void LoadValue(string value)
        {
            tb.Text = value;
        }

        public GSInput(GameSettingControl self)
        {
            this.self = self;
            tb = new TextBox();
        }

        public Control GetComponent()
        {
            if (!string.IsNullOrEmpty(self.defaultValue))
                tb.Text = self.defaultValue;

            if(!string.IsNullOrEmpty(self.PlaceHolder))
                tb.SetValue(MaterialDesignThemes.Wpf.HintAssist.HintProperty, self.PlaceHolder);

            if (self.Width > -1)
                tb.Width = self.Width;

            return tb;
        }

        public void Discard()
        {
            self = null;
        }

        public string GetParam()
        {
            if (self.canBeBlank && string.IsNullOrWhiteSpace(tb.Text))
                return null;

            return self.Command.Replace("$", tb.Text);
        }
    }
}
