namespace SteamCMDLauncher.Component
{
    public static class EventHooks
    {
        #region GameSettingManager Event

        public delegate void view_hint_dialog(string hint);

        /// <summary>
        /// Event used for GSM to show hints (if control supports it)
        /// </summary>
        public static event view_hint_dialog View_Dialog;

        public static void UnhookHint()
        {
            Config.Log("Unhooking \"UnhookHint\"");
            View_Dialog -= View_Dialog;
        }

        public static void InvokeHint(string hint)
        {
            Config.Log("Invoking \"UnhookHint\"");
            View_Dialog.Invoke(hint);
        }

        #endregion

        #region ServerCard Event
        // Function which describes how the event is triggered with arguments
        public delegate void view_server_func(string unique_id);

        // Function which describes how the event triggers to open the server folder
        public delegate void view_folder(string unique_id, string location);

        public static event view_server_func View_Server;
        public static event view_folder View_Folder;
        
        public static void UnhookServerCardEvents()
        {
            Config.Log("Unhooking \"InvokeServerCard\"");
            View_Folder -= View_Folder;
            View_Server -= View_Server;
        }

        public static void InvokeServerCard(string id, string location = null)
        {
            Config.Log("Invoking \"InvokeServerCard\"");

            if (string.IsNullOrEmpty(location))
                View_Server.Invoke(id);
            else
                View_Folder.Invoke(id, location);
        }
        #endregion

    }
}
