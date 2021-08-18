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

        /// <summary>
        /// Turns if the component is empty, useful for components which cannot be left blank
        /// </summary>
        public abstract bool IsEmpty { get; }

        /// <summary>
        /// Used when storing variables
        /// </summary>
        public abstract string SaveValue { get; }

        /// <summary>
        /// Loads a value from a config file
        /// </summary>
        /// <param name="value">Value to evaulate to the desired control</param>
        public abstract void LoadValue(string value);
    }
}
