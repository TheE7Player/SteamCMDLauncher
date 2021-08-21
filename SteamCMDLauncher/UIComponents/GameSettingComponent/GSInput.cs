using System.Windows.Controls;

namespace SteamCMDLauncher.UIComponents.GameSettingComponent
{
    class GSInput : ISettingConstruct
    {
        private TextBox tb;

        public GameSettingControl self { get; set; }

        public string GetControlType => "gsinput";

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

        public string WriteFilePath { get; private set; }

        public GSInput(GameSettingControl self)
        {
            this.self = self;
            tb = new TextBox();
        }

        public void SetWritePath(string path) => WriteFilePath = path;

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

        public string GetParam(string info = null)
        {
            if (self.canBeBlank && string.IsNullOrWhiteSpace(tb.Text))
                return null;

            if(!string.IsNullOrWhiteSpace(WriteFilePath))
            {
                if (string.IsNullOrWhiteSpace(info))
                    throw new System.Exception("GSInput.GetParam didn't return a path, WriteFilePath requires this.");

                string path = string.Concat(info, WriteFilePath);

                if (!System.IO.File.Exists(path))
                    throw new System.Exception($"GSInput.GetParam didn't join the absolute path correctly, got: \"{path}\"");
                try
                {
                    Config.Log($"[GSInput] Writing to file {path}, contents: {tb.Text} with separator ';'");
                    System.IO.File.WriteAllLines(path, tb.Text.Trim().Split(';'));
                    Config.Log("[GSInput] Writing to file was complete");
                }
                catch (System.Exception ex)
                {
                    Config.Log($"[GSInput] Exception hit from writing to file: {ex.Message}");
                    Config.Log($"[GSInput] Failed to write to file: '{path}'. File is already in use or doesn't have permissions to do so. Continuing on anyways...");
                }

                path = null;

                return null;
            }
           
            return self.Command.Replace("$", tb.Text);
        }
    }
}
