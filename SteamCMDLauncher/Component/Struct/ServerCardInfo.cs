namespace SteamCMDLauncher.Component.Struct
{
    public struct ServerCardInfo
    {
        /*~ServerCardInfo()
        {
            Config.Log($"[SCI] Card deconstructor was called for: {Unique_ID}");
            Unique_ID = null;
            GameID = null;
            Alias = null;
            Folder = null;
        }*/

        public string Unique_ID;
        public string GameID;
        public string Alias;
        public string Folder;

        public bool IsEmpty => string.IsNullOrWhiteSpace(Unique_ID)
        || string.IsNullOrWhiteSpace(GameID)
        || string.IsNullOrWhiteSpace(Folder);
    }
}
