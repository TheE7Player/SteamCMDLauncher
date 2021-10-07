using System;

namespace SteamCMDLauncher.Component.Struct
{
    public class ServerLogBinding
    {
        private string date;
        private string type;

        public string Date { get => GetLocalTime(date); set => date = value; }
        public string Type { get => GetType(type); set => type = value; }
        public string Reason { get; set; }

        private string GetType(string type)
        {
            _ = Enum.TryParse(type, out Config.LogType t);

            string output = t switch
            {
                Config.LogType.ServerAdd => "Add",
                Config.LogType.FolderChange => "Folder",
                Config.LogType.AliasChange => "Alias",
                Config.LogType.ServerRun => "Run",
                Config.LogType.ServerStop => "Stop",
                Config.LogType.ServerError => "Error",
                Config.LogType.ServerValidate => "Validation",
                Config.LogType.ServerUpdate => "Updating",
                Config.LogType.ServerRemove => "Removal",
                _ => "UNK"
            };

            type = null;

            return output;
        }

        private string GetLocalTime(string date)
        {
            string utc_format = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'";
            
            DateTime parsedDate = DateTime.ParseExact(date, utc_format, System.Globalization.CultureInfo.InvariantCulture);

            // Correct the time into true local zone
            parsedDate = TimeZoneInfo.ConvertTimeFromUtc(parsedDate, TimeZoneInfo.Local);

            utc_format = null;
            date = null;

            return parsedDate.ToString("G");
        }
    }
}
