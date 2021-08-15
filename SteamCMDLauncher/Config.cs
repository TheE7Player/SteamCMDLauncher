using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using LiteDB;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SteamCMDLauncher
{
    public static class Config
    {
        #region Variables/Constants
        private static readonly string db_location = System.IO.Path.Combine(Environment.CurrentDirectory, "data.db");

        private static string db_error = string.Empty;

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

        private static Queue<BsonDocument> LogQueue;

        //TODO: Make file if closing and stuff is still setting on LoqQueue (QueueRunner)
        private static Timer QueueRunner;
        private const int QueueRunner_Wait = 4000;

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
            db_error = string.Empty;

            ILiteCollection<BsonDocument> col;

            try
            {
                using (var db = new LiteDatabase(db_location))
                {
                    col = db.GetCollection(collection);

                    var current_table = col.FindAll().ToArray();

                    col.Insert(new BsonDocument { ["_id"] = GetID(), [key] = new BsonValue(value) });
                }

                return true;
            }
            catch (Exception _)
            {
               db_error = _.Message;
               return false;
            }
        }

        public static BsonValue GetEntryByKey(string key, string collection)
        {
            db_error = string.Empty;

            ILiteCollection<BsonDocument> col;

            try
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
            catch (Exception _)
            {
                db_error = _.Message;
            }

            return null;
        }

        public static bool AddServer(int id, string folder_loc)
        {
            db_error = string.Empty;

            ILiteCollection<BsonDocument> col;

            try
            {
                using (var db = new LiteDatabase(db_location))
                {
                    col = db.GetCollection(SERVER_INFO_COLLECTION);

                    string u_id = GetID();
                    
                    col.Insert(new BsonDocument { ["_id"] = u_id, ["app_id"] = id, ["folder"]=folder_loc});

                    if (!Require_Get_Server)
                        Require_Get_Server = true;

                    AddLog(u_id, LogType.ServerAdd, $"New server added with ID: {u_id}");

                    return true;
                }
            }
            catch (Exception _)
            {
                db_error = _.Message;
            }

            return false;
        }

        public static bool RemoveServer(string id)
        {
            db_error = string.Empty;

            ILiteCollection<BsonDocument> col;

            try
            {
                using (var db = new LiteDatabase(db_location))
                {
                    col = db.GetCollection(SERVER_INFO_COLLECTION);

                    int eee = col.DeleteMany(x => x["_id"] == id);

                    if (eee == 0)
                    {
                        db_error = "Couldn't find any server with given ID";
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
            catch (Exception _)
            {
                db_error = _.Message;
            }

            return false;
        }

        public static bool HasServers()
        {
            db_error = string.Empty;

            ILiteCollection<BsonDocument> col;

            try
            {
                using (var db = new LiteDatabase(db_location))
                {
                    col = db.GetCollection(SERVER_INFO_COLLECTION);

                    return col.FindAll().Count() > 0;
                }
            }
            catch (Exception _)
            {
                db_error = _.Message;
            }

            return false;
        }

        public static Dictionary<string, string[]> GetServers()
        {
            db_error = string.Empty;

            ILiteCollection<BsonDocument> col;
            var _dict = new Dictionary<string, string[]>();

            try
            {
                using (var db = new LiteDatabase(db_location))
                {
                    col = db.GetCollection(SERVER_INFO_COLLECTION);
                    var aliases = db.GetCollection(SERVER_ALIAS_COLLECTION);

                    var servers = col.FindAll();

                    BsonDocument alias;

                    if(!(servers is null))
                    foreach (var item in servers)
                    { 
                        alias = aliases.FindOne(Query.EQ("_id", item["_id"]));
                        _dict.Add(item["_id"], new string[] { item["app_id"].RawValue.ToString(), item["folder"], (alias is null) ? string.Empty: alias["alias"].AsString, item["installed"] });
                    }

                    if (Require_Get_Server) Require_Get_Server = false;

                    return _dict;
                }
            }
            catch (Exception _)
            {
                db_error = _.Message;
            }

            return null;
        }

        public static string GetGameByAppId(string id)
        {
            string file = System.Text.Encoding.Default.GetString(SteamCMDLauncher.Properties.Resources.dedicated_server_list);

            Newtonsoft.Json.Linq.JObject objectA = Newtonsoft.Json.Linq.JObject.Parse(file);

            file = null;

            return objectA["server"]
                 .Children()
                 .Where(x => x["id"].ToString() == id)
                 .Single()
                 .Value<string>("game");
        }

        public static string GetCMDDirectory()
        {
            db_error = string.Empty;

            ILiteCollection<BsonDocument> col;

            try
            {
                using (var db = new LiteDatabase(db_location))
                {
                    col = db.GetCollection(INFO_COLLECTION);

                    var exe = col.FindOne(Query.Contains("cmd", ":"));

                    return (exe is null) ? string.Empty : exe["cmd"].AsString;
                }
            }
            catch (Exception _)
            {
                db_error = _.Message;
            }

            return string.Empty;
        }

        public static bool ChangeServerAlias(string id, string new_alias)
        {
            db_error = string.Empty;

            ILiteCollection<BsonDocument> col;

            try
            {
                using (var db = new LiteDatabase(db_location))
                {
                    col = db.GetCollection(SERVER_ALIAS_COLLECTION);

                    var r = col.FindById(id);

                    var old_alias = (r is null) ? string.Empty : r["alias"].AsString;

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
            catch (Exception _)
            {
                db_error = _.Message;
            }

            return false;
        }

        public static bool ChangeServerFolder(string id, string old_location, string new_location)
        {
            db_error = string.Empty;

            ILiteCollection<BsonDocument> col;

            try
            {
                using (var db = new LiteDatabase(db_location))
                {
                    col = db.GetCollection(SERVER_INFO_COLLECTION);

                    var r = col.FindOne(Query.EQ("folder", old_location));

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
            catch (Exception _)
            {
                db_error = _.Message;
            }

            return false;
        }

        //TODO: Use a lock or mutex for db connections (if db is already in use)

        public static bool CleanLog()
        {
            try
            {
                Log("Clearing log table");
                using (var db = new LiteDatabase(db_location))
                {
                    var col = db.GetCollection(LOG_COLLECTION);

                    return col.DeleteAll() > 0;
                }
            }
            catch (Exception _)
            {
                db_error = _.Message;
                return false;
            }
        }

        public static void RunLogQueue()
        {
            if (LogQueue is null) return;
            if (LogQueue.Count == 0) return;

            if(!(QueueRunner is null))
                QueueRunner.Stop();

            db_error = string.Empty;

            ILiteCollection<BsonDocument> col;

            try
            {
                Log("Runner Queue Log to DB");
                using (var db = new LiteDatabase(db_location))
                {
                    col = db.GetCollection(LOG_COLLECTION);

                    BsonDocument item;
                    while (LogQueue.Count > 0)
                    {
                        item = LogQueue.Dequeue();
                        col.Insert(item);
                    }

                    col.EnsureIndex("svr_id");
                }
            }
            catch (Exception _)
            {
                db_error = _.Message;
            }
        }
    
        public static void AddLog(string id, LogType lType, string details)
        {
            if (LogQueue is null)
                LogQueue = new Queue<BsonDocument>();

            if (QueueRunner is null)
            { 
                QueueRunner = new Timer(QueueRunner_Wait);
                QueueRunner.AutoReset = true;
                QueueRunner.Elapsed += (_, e) =>
                {
                    RunLogQueue();
                };
            }
            
            QueueRunner.Start();

            LogQueue.Enqueue(new BsonDocument
            {
                ["svr_id"] = id,
                ["time"] = DateTime.Now.UTC_String(),
                ["type"] = ((int)lType),
                ["info"] = details
            });
        }

        public static string[] FindGameID(string path)
        {
            var rList = new List<string>(10);

            JObject CurrentFiles = JObject.Parse(SteamCMDLauncher.Properties.Resources.server_file_search);
            string current_app_id;

            // .Name -> Key
            // .Value -> files ( '/' for folder, '.' for exe )
            // Detection for exe, even if its absolute path ( / with an .exe or . )

            bool found = false;
            string val;
            string joined_path;
            foreach (var game in CurrentFiles)
            {
                current_app_id = game.Key;

                // Loop through each available folder or exe to find the game
                foreach (var item in game.Value)
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

        public static void Log(string text) => System.Diagnostics.Debug.WriteLine(text);
        #endregion
    }
}