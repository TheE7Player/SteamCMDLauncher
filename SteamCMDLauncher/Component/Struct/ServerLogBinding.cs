using System;

namespace SteamCMDLauncher.Component.Struct
{
    public class ServerLogBinding
    {
        private string date;
        private string type;

        public string Date { get { return GetLocalTime(date); } set { date = value; } }
        public string Type { get { return GetType(type); } set { type = value; } }
        public string Reason { get; set; }

        private string GetType(string type)
        {
            Config.LogType t;

            string output = string.Empty;

            if (!Enum.TryParse<Config.LogType>(type, out t))
                return "UNK";

            switch (t)
            {
                case Config.LogType.ServerAdd:
                    output = "Add"; break;
                case Config.LogType.FolderChange:
                    output = "Folder"; break;
                case Config.LogType.AliasChange:
                    output = "Alias"; break;
                case Config.LogType.ServerRun:
                    output = "Run"; break;
                case Config.LogType.ServerStop:
                    output = "Stop"; break;
                case Config.LogType.ServerError:
                    output = "Error"; break;
                case Config.LogType.ServerValidate:
                    output = "Validation"; break;
                case Config.LogType.ServerUpdate:
                    output = "Updating"; break;
                case Config.LogType.ServerRemove:
                    output = "Removal"; break;
            }

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
