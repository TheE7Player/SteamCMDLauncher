using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;

namespace SteamCMDLauncher.UIComponents.GameSettingComponent
{
    class GSPass : ISettingConstruct
    {
        PasswordBox pb;

        public GameSettingControl self { get; set; }

        public bool IsEmpty
        {
            get
            {
                if (pb is null) return true;

                if (pb.Password.Length < 1) return true;

                return false;
            }
        }

        public bool UsingAutoPass { get; set; }

        public string AutoPass { get; set; }

        public GSPass(GameSettingControl self)
        {
            this.self = self;
            
            pb = new PasswordBox();
            
            UsingAutoPass = true;

            Config.Log("[GSM] Generating Random Password");

            var sb = new StringBuilder();

            Random ran = new Random((int)DateTime.Now.Ticks);

            int len = ran.Next(8, 15);

            int char_min = 33;
            int char_max = 126;

            for (int i = 0; sb.Length <= len; i++)
            {
                sb.Append((char)ran.Next(char_min, char_max));
            }

            AutoPass = sb.ToString();

            Config.Log("[GSM] Random password is set and waiting, if used.");

            sb.Clear(); sb = null;
        }

        public Control GetComponent()
        {
            if (!string.IsNullOrEmpty(self.PlaceHolder))
                pb.SetValue(MaterialDesignThemes.Wpf.HintAssist.HintProperty, self.PlaceHolder);

            if (self.Width > -1) pb.Width = self.Width;

            pb.PasswordChanged += (s, e) =>
            {
                PasswordBox pw = (PasswordBox)s;

                UsingAutoPass = pw.Password.Length == 0;
            };

            return pb;
        }

        public void Discard()
        {
            self = null;
        }

        public string GetParam()
        {
            return self.Command.Replace("$", UsingAutoPass ? AutoPass : pb.Password);
        }
    }
}