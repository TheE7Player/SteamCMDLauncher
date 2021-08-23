using System;
using System.Text;
using System.Windows.Controls;

namespace SteamCMDLauncher.UIComponents.GameSettingComponent
{
    class GSPass : ISettingConstruct
    {
        PasswordBox pb;

        public GameSettingControl self { get; set; }

        public string GetControlType => "gspass";

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

        public string SaveValue { 
            get {

                if (IsEmpty) return null;

                Component.Encryption encr = new Component.Encryption();

                return encr.AES_Encrypt(pb.Password);
            } 
        }

        public void LoadValue(string value)
        {
            Component.Encryption encr = new Component.Encryption();

            pb.Password = encr.AES_Decrypt(value);

            encr = null;
        }

        public GSPass(GameSettingControl self)
        {
            this.self = self;
            
            pb = new PasswordBox();
            
            UsingAutoPass = true;

            Config.Log("[GSM] Generating Random Password");

            StringBuilder sb = new StringBuilder();

            Random ran = new Random((int)DateTime.Now.Ticks);

            int len = ran.Next(8, 15);

            int char_min = 33;
            int char_max = 126;

            char selectedChar;

            for (int i = 0; sb.Length <= len; i++)
            {
                // Prevent unusual characters generating in auto-pass
                while (true)
                {
                    selectedChar = (char)ran.Next(char_min, char_max);
                    
                    if(char.IsLetterOrDigit((selectedChar)))
                        break;
                }
                
                sb.Append(selectedChar);
            }

            AutoPass = sb.ToString();

            Config.Log("[GSM] Random password is set and waiting, if used.");

            sb = null;
            ran = null;
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
            pb = null;
        }

        public string GetParam(string info = null)
        {
            return self.Command.Replace("$", UsingAutoPass ? AutoPass : pb.Password);
        }
    }
}