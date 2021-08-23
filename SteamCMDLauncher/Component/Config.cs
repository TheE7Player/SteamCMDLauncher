using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using Newtonsoft.Json.Linq;

namespace SteamCMDLauncher
{
    public static class Config
    {
        #region Variables/Constants
        private static readonly string db_location = System.IO.Path.Combine(Environment.CurrentDirectory, "data.db");

        public const string INFO_COLLECTION = "ifo";
        public const string LOG_COLLECTION = "lg";
        public const string SERVER_INFO_COLLECTION = "sci";
        public const string SERVER_ALIAS_COLLECTION = "sca";

        public enum LogType
        {
            // Server appending codes
            ServerAdd = 0,

            // Server location and naming codes
            FolderChange = 1, AliasChange = 2,

            // Server running/status codes
            ServerRun = 3, ServerStop = 4, ServerError = 5, ServerValidate = 6, ServerUpdate = 7, ServerRemove = 8
        }

        private static Component.AutoFlushQueue<BsonDocument> LogQueue;
        private const int QueueRunner_Wait = 2000;

        private static string SessionFileName = string.Empty;
        private static readonly object db_lock = new object();

        #endregion

        #region Properties
        public static bool Require_Get_Server { get; private set; } = false;

        public static bool DatabaseExists => System.IO.File.Exists(db_location);

        public static string DatabaseLocation => db_location;
        #endregion

        #region Server Related
        private static string GetID() => ObjectId.NewObjectId().ToString();

        /// <summary>
        /// Returns true if the adding of the item was successful, false if not
        /// </summary>
        /// <param name="key">The key the item will have</param>
        /// <param name="value">The value assigned to the given key</param>
        /// <param name="collection">The table (collection) to store it under</param>
        /// <returns></returns>
        public static bool AddEntry_BJSON(string key, object value, string collection)
        {


            ILiteCollection<BsonDocument> col;

            try
            {
                lock (db_lock)
                {
                    using (var db = new LiteDatabase(db_location))
                    {
                        col = db.GetCollection(collection);

                        var current_table = col.FindAll().ToArray();

                        col.Insert(new BsonDocument { ["_id"] = GetID(), [key] = new BsonValue(value) });
                    }

                    return true;
                }
            }
            catch (Exception _)
            {
               throw new Exception(_.Message);
            }
        }

        public static BsonValue GetEntryByKey(string key, string collection)
        {
            ILiteCollection<BsonDocument> col;

            try
            {
                lock (db_lock)
                {
                    using (var db = new LiteDatabase(db_location))
                    {
                        col = db.GetCollection(collection);

                        var current_table = col.FindAll().ToArray();

                        var entries = current_table.FirstOrDefault(x => x.Keys.Contains(key));

                        if (entries is null) return null;

                        return entries[key];
                    } 
                }
            }
            catch (Exception _)
            {
                throw new Exception(_.Message);
            }
        }

        public static bool AddServer(int id, string folder_loc)
        {

            ILiteCollection<BsonDocument> col;

            try
            {
                lock (db_lock)
                {
                    using (var db = new LiteDatabase(db_location))
                    {
                        col = db.GetCollection(SERVER_INFO_COLLECTION);

                        string u_id = GetID();

                        col.Insert(new BsonDocument { ["_id"] = u_id, ["app_id"] = id, ["folder"] = folder_loc });

                        if (!Require_Get_Server)
                            Require_Get_Server = true;

                        AddLog(u_id, LogType.ServerAdd, $"New server added with ID: {u_id}");

                        return true;
                    } 
                }
            }
            catch (Exception _)
            {
                throw new Exception(_.Message);
            }
        }

        public static bool RemoveServer(string id)
        {

            ILiteCollection<BsonDocument> col;

            try
            {

                lock (db_lock)
                {
                    using (var db = new LiteDatabase(db_location))
                    {
                        col = db.GetCollection(SERVER_INFO_COLLECTION);

                        int eee = col.DeleteMany(x => x["_id"] == id);

                        if (eee == 0)
                        {
                            Log($"[Remove Server] Couldn't find any server with given ID: '{id}'");
                            return false;
                        }

                        col = db.GetCollection(SERVER_ALIAS_COLLECTION);

                        // We don't care much if this one fails - but should it be considered?
                        col.DeleteMany(x => x["_id"] == id);

                        db.Rebuild();

                        AddLog(id, LogType.ServerRemove, $"Deleted server with ID: {id}");

                        return true;
                    } 
                }
            }
            catch (Exception _)
            {
                throw new Exception(_.Message);
            }
        }

        public static bool HasServers()
        {


            ILiteCollection<BsonDocument> col;

            try
            {
                lock (db_lock)
                {
                    using (var db = new LiteDatabase(db_location))
                    {
                        col = db.GetCollection(SERVER_INFO_COLLECTION);

                        return col.FindAll().Count() > 0;
                    }
                }
            }
            catch (Exception _)
            {
                throw new Exception(_.Message);
            }
        }

        public static Component.Struct.ServerCardInfo[] GetServersNew()
        {
            Span<Component.Struct.ServerCardInfo> server_info = new Span<Component.Struct.ServerCardInfo>(new Component.Struct.ServerCardInfo[10]);

            ILiteCollection<BsonDocument> col;
            ILiteCollection<BsonDocument> aliases;
            IEnumerable<BsonDocument> servers;
            BsonDocument alias;

            int spanIndex = 0;

            try
            {
                lock (db_lock)
                {
                    using (var db = new LiteDatabase(db_location))
                    {
                        col = db.GetCollection(SERVER_INFO_COLLECTION);
                        aliases = db.GetCollection(SERVER_ALIAS_COLLECTION);
                        servers = col.FindAll();
                      
                        if (!(servers is null))
                            foreach (BsonDocument item in servers)
                            {
                                alias = aliases.FindOne(Query.EQ("_id", item["_id"]));

                                server_info[spanIndex] = new Component.Struct.ServerCardInfo()
                                {
                                    Unique_ID = item["_id"],
                                    GameID = item["app_id"].RawValue.ToString(),
                                    Folder = item["folder"],
                                    Alias = alias is null ? string.Empty : alias["alias"].AsString
                                };
                                spanIndex++;                               
                            }

                        if (Require_Get_Server) Require_Get_Server = false;                      
                    }
                }
            }
            catch (Exception _)
            {
                throw new Exception(_.Message);
            }

            // Time to resize the span
            server_info = server_info.Slice(0, spanIndex);

            return server_info.ToArray();
        }

        public static Dictionary<string, string[]> GetServers()
        {
            ILiteCollection<BsonDocument> col;
            Dictionary<string, string[]> _dict = new Dictionary<string, string[]>();

            try
            {
                lock (db_lock)
                {
                    using (var db = new LiteDatabase(db_location))
                    {
                        col = db.GetCollection(SERVER_INFO_COLLECTION);
                        ILiteCollection<BsonDocument> aliases = db.GetCollection(SERVER_ALIAS_COLLECTION);

                        IEnumerable<BsonDocument> servers = col.FindAll();

                        BsonDocument alias;

                        if (!(servers is null))
                            foreach (BsonDocument item in servers)
                            {
                                alias = aliases.FindOne(Query.EQ("_id", item["_id"]));
                                _dict.Add(item["_id"], new string[] { item["app_id"].RawValue.ToString(), item["folder"], (alias is null) ? string.Empty : alias["alias"].AsString, item["installed"] });
                            }

                        if (Require_Get_Server) Require_Get_Server = false;

                        return _dict;
                    } 
                }
            }
            catch (Exception _)
            {
                throw new Exception(_.Message);
            }
        }

        public static string GetGameByAppId(string id)
        {
            string file = System.Text.Encoding.Default.GetString(SteamCMDLauncher.Properties.Resources.dedicated_server_list);

            Newtonsoft.Json.Linq.JObject jsonObject = Newtonsoft.Json.Linq.JObject.Parse(file);

            file = null;

            JToken GameEntity = jsonObject["server"]
                 .Children()
                 .FirstOrDefault(x => x["id"].ToString() == id);

            jsonObject = null;

            return GameEntity is null ? string.Empty : GameEntity["game"].ToString();
        }

        public static string GetCMDDirectory()
        {


            ILiteCollection<BsonDocument> col;

            try
            {
                lock (db_lock)
                {
                    using (var db = new LiteDatabase(db_location))
                    {
                        col = db.GetCollection(INFO_COLLECTION);

                        BsonDocument exe = col.FindOne(Query.Contains("cmd", ":"));

                        return (exe is null) ? string.Empty : exe["cmd"].AsString;
                    } 
                }
            }
            catch (Exception _)
            {
                throw new Exception(_.Message);
            }
        }

        public static bool ChangeServerAlias(string id, string new_alias)
        {


            ILiteCollection<BsonDocument> col;

            try
            {
                lock (db_lock)
                {
                    using (var db = new LiteDatabase(db_location))
                    {
                        col = db.GetCollection(SERVER_ALIAS_COLLECTION);

                        BsonDocument r = col.FindById(id);

                        string old_alias = (r is null) ? string.Empty : r["alias"].AsString;

                        // Perform "INSERT" query if doesn't exist, else perform "UPDATE" query
                        if (r is null)
                        {
                            col.Insert(new BsonDocument { ["_id"] = id, ["alias"] = new_alias });
                        }
                        else
                        {
                            r["alias"] = new_alias;
                            col.Update(r);
                        }

                        AddLog(id, LogType.AliasChange, $"From: '{old_alias}', To: '{new_alias}'");

                        return !(r is null);
                    } 
                }
            }
            catch (Exception _)
            {
                throw new Exception(_.Message);
            }
        }

        public static bool ChangeServerFolder(string id, string old_location, string new_location)
        {


            ILiteCollection<BsonDocument> col;

            try
            {
                lock (db_lock)
                {
                    using (var db = new LiteDatabase(db_location))
                    {
                        col = db.GetCollection(SERVER_INFO_COLLECTION);

                        BsonDocument r = col.FindOne(Query.EQ("folder", old_location));

                        // Perform "INSERT" query if doesn't exist, else perform "UPDATE" query
                        if (r is null)
                            col.Insert(new BsonDocument { ["_id"] = id, ["folder"] = new_location });
                        else
                        {
                            r["folder"] = new_location;
                            col.Update(r);
                        }

                        AddLog(id, LogType.FolderChange, $"Changed to: '{new_location}'");

                        return !(r is null);
                    } 
                }
            }
            catch (Exception _)
            {
                throw new Exception(_.Message);
            }
        }

        public static bool CleanLog()
        {
            try
            {
                lock (db_lock)
                {
                    Log("Clearing log table");
                    using (var db = new LiteDatabase(db_location))
                    {
                        var col = db.GetCollection(LOG_COLLECTION);

                        return col.DeleteAll() > 0;
                    } 
                }
            }
            catch (Exception _)
            {
                throw new Exception(_.Message);
            }
        }

        public static void RunLogQueue(BsonDocument[] elem)
        {
            if (elem is null) return;

            ILiteCollection<BsonDocument> col;

            try
            {
                lock (db_lock)
                {
                    Log("Runner Queue Log to DB");

                    using (var db = new LiteDatabase(db_location))
                    {
                        col = db.GetCollection(LOG_COLLECTION);

                        for (int i = 0; i < elem.Length; i++)
                        {
                            col.Insert(elem[0]);
                        }

                        col.EnsureIndex("svr_id");
                    } 
                }
            }
            catch (Exception _)
            {
                throw new Exception(_.Message);
            }
        }
    
        public static void AddLog(string id, LogType lType, string details)
        {         
            if (LogQueue is null)
            { 
                LogQueue = new Component.AutoFlushQueue<BsonDocument>(4, QueueRunner_Wait);
                LogQueue.OnFlushElapsed = RunLogQueue;
            }

            LogQueue.Add(new BsonDocument
            {
                ["svr_id"] = id,
                ["time"] = DateTime.Now.UTC_String(),
                ["type"] = ((int)lType),
                ["info"] = details
            });
        }

        public static string[] FindGameID(string path)
        {
            List<string> rList = new List<string>(10);

            JObject CurrentFiles = JObject.Parse(SteamCMDLauncher.Properties.Resources.server_file_search);
            string current_app_id;

            // .Name -> Key
            // .Value -> files ( '/' for folder, '.' for exe )
            // Detection for exe, even if its absolute path ( / with an .exe or . )

            bool found = false;
            string val;
            string joined_path;

            foreach (KeyValuePair<string, JToken> game in CurrentFiles)
            {
                current_app_id = game.Key;

                // Loop through each available folder or exe to find the game
                foreach (JToken item in game.Value)
                {
                    val = item.ToString();

                    // Join the path and remove the first symbol ( / or . )
                    joined_path = Path.Combine(path, val.Substring(1, val.Length-1));

                    found = (val.StartsWith("/") && !val.Contains(".")) ? 
                            Directory.Exists(joined_path) :
                            File.Exists(joined_path);

                    if (found)
                    {
                        rList.Add(current_app_id); break;
                    };
                }

            }

            val = null;
            joined_path = null;

            return rList.ToArray();
        }

        #endregion

        #region Utilities
        public static string GetFolder(string required_file, string rule_break)
        {
            var dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog();
            dialog.InitialDirectory = "C:\\Users";
            dialog.IsFolderPicker = true;

            while (true)
            {
                if (dialog.ShowDialog() == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
                {
                    if (!string.IsNullOrEmpty(required_file))
                        if (!System.IO.File.Exists(System.IO.Path.Combine(dialog.FileName, required_file)))
                        {
                            System.Windows.MessageBox.Show(rule_break);
                            continue;
                        }
                    return dialog.FileName;
                }
                else { break; }
            }

            return string.Empty;
        }

        public static string GetFolder(string required_file, Action custom_action)
        {
            var dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog();
            dialog.InitialDirectory = "C:\\Users";
            dialog.IsFolderPicker = true;

            while (true)
            {
                if (dialog.ShowDialog() == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
                {
                    if (!string.IsNullOrEmpty(required_file))
                        if (!System.IO.File.Exists(System.IO.Path.Combine(dialog.FileName, required_file)))
                        {
                            custom_action();
                            continue;
                        }
                    return dialog.FileName;
                }
                else { break; }
            }

            return string.Empty;
        }

        /// <summary>
        /// Opens the file dialog, expecting the type requested only
        /// </summary>
        /// <param name="target_type">The file extension to find (.txt, .cfg etc)</param>
        /// <returns>Absolute Path to the file, null if cancelled</returns>
        public static string GetFile(string target_type)
        {
            var dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog();

            string target_dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "configs");

            if (!Directory.Exists(target_dir))
                Directory.CreateDirectory(target_dir);

            dialog.InitialDirectory = target_dir;

            dialog.Title = "Select the .cfg you need";
            dialog.EnsureFileExists = true;
            dialog.EnsurePathExists = true;

            while (true)
            {
                if (dialog.ShowDialog() == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
                {
                    if(Path.GetExtension(dialog.FileName) == target_type)
                        return dialog.FileName;
                }
                else { break; }
            }

            return string.Empty;
        }

        public static void Log(string text) 
        {
            #if RELEASE
                if(string.IsNullOrEmpty(SessionFileName))
                {
                    string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

                    if (!Directory.Exists(logFolder))
                        Directory.CreateDirectory(logFolder);

                    int currentNumber = Directory.GetFiles(logFolder).Length + 1;
                    SessionFileName = Path.Combine(logFolder, $"scmdl_session_{currentNumber}.txt");
                }

                using (StreamWriter sw = File.AppendText(SessionFileName))
                {
                    sw.WriteLine($"[{DateTime.Now.ToShortDateString()} {DateTime.Now.ToLongTimeString()}] : {text}");
                }
            #else
                System.Diagnostics.Debug.WriteLine(text);
            #endif
        }
#endregion
    }
}