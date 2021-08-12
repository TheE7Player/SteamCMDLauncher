using System;
using System.Collections.Generic;
using System.Text;

namespace SteamCMDLauncher.UIComponents
{
    interface ISettingControl
    {
        public string Heading { get; set; }
        public string Hint { get; set; }
        public string Command { get; set; }
        public string defaultValue { get; set; }
        public string PlaceHolder { get; set; }
  
        public bool canBeBlank { get; set; }

        public double Width { get; set; }
    }
}
