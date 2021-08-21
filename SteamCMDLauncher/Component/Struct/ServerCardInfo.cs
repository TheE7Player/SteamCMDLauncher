namespace SteamCMDLauncher.Component.Struct
{
    public struct ServerCardInfo
    {
        public string Unique_ID;
        public string GameID;
        public string Alias;
        public string Folder;

        public bool IsEmpty => string.IsNullOrWhiteSpace(Unique_ID)
        || string.IsNullOrWhiteSpace(GameID)
        || string.IsNullOrWhiteSpace(Alias)
        || string.IsNullOrWhiteSpace(Folder);
    }
}
