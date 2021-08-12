using System;
using System.Collections.Generic;
using System.Text;

namespace SteamCMDLauncher.UIComponents
{
    interface ISettingConstruct
    {
        /// <summary>
        /// The instace of the properties to read from/handle
        /// </summary>
        public abstract GameSettingControl self { get; set; }
        
        /// <summary>
        /// Get the constructed element
        /// </summary>
        /// <returns></returns>
        public abstract System.Windows.Controls.Control GetComponent();
        
        /// <summary>
        /// Null any hidden attributes to make GC handling easier
        /// </summary>
        public abstract void Discard();
        
        /// <summary>
        /// Returns the command based on the reflected values
        /// </summary>
        /// <returns></returns>
        public abstract string GetParam();
    }
}
