using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using Newtonsoft.Json.Linq;

namespace SteamCMDLauncher
{
    public class Config
    {
        #region Variables/Constants
        private static readonly string db_location = System.IO.Path.Combine(Environment.CurrentDirectory, "data.db");

        public const string INFO_COLLECTION = "ifo";
        public const string LOG_COLLECTION = "lg";
        public const string SERVER_INFO_COLLECTION = "sci";
        public const string SERVER_ALIAS_COLLECTION = "sca";      
        public const string SERVER_BUILDV_COLLECTION = "sbv";

        public enum LogType
        {
            // Server appending codes
            ServerAdd = 0,

            // Server location and naming codes
            FolderChange = 1, AliasChange = 2,

            // Server running/status codes
            ServerRun = 3, ServerStop = 4, ServerError = 5, ServerValidate = 6, ServerUpdate = 7, ServerRemove = 8,

            // Server Update codes
            ServerOutOfDate = 9, ServerUpdated = 10
        }

        private static Component.AutoFlushQueue<BsonDocument> LogQueue;
        private const int QueueRunner_Wait = 2000;

        private static string SessionFileName = string.Empty;

        #endregion

        #region Properties
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
        public bool AddEntry_BJSON(string key, object value, string collection)
        {
            Component.DBManager db = new Component.DBManager(db_location);

            BsonDocument insert_value = new BsonDocument { ["_id"] = GetID(), [key] = new BsonValue(value) };

            bool result = db.Insert(collection, insert_value);

            db.Destory();
            
            insert_value = null;
            
            db = null;

            return result;
        }

        public BsonValue GetEntryByKey(string key, string collection)
        {
            Component.DBManager db = new Component.DBManager(db_location);

            BsonValue item = ((BsonValue)db.FilterKey(collection, key).Target);

            db.Destory();

            collection = null;
            db = null;

            return (item == null) ? null : item[key];
        }

        public bool AddServer(int id, string folder_loc)
        {
            Component.DBManager db = new Component.DBManager(db_location);

            string u_id = GetID();
            
            BsonDocument eee = new BsonDocument { ["_id"] = u_id, ["app_id"] = id, ["folder"] = folder_loc };

            if(!db.Insert(SERVER_INFO_COLLECTION, eee)) throw new Exception(db.Reason);
            
            AddLog(u_id, LogType.ServerAdd, $"New server added with ID: {u_id}");
           
            db.Destory();
            
            u_id = null;
            eee = null;
            db = null;
            folder_loc = null;

            return true;
        }

        public bool RemoveServer(string id)
        {
            Component.DBManager db = new Component.DBManager(db_location);
            
            // Remove the server location and ID
            if(!db.RemoveMany(id, SERVER_INFO_COLLECTION))
            {
                Log($"[Remove Server] Failed to remove server details: {db.Reason}");
                return false;
            }

            // Remove the server alias (if changed)
            db.RemoveMany(id, SERVER_ALIAS_COLLECTION);

            // Remove all the logging information (actions) from this database
            db.RemoveMany(id, LOG_COLLECTION, "svr_id");

            // Remove current build server versions if any
            db.RemoveMany(id, SERVER_BUILDV_COLLECTION, "_id");

            db.Destory();
            
            AddLog(id, LogType.ServerRemove, $"Deleted server with ID: {id}");
           
            id = null;
            db = null;

            return true;
        }

        public static bool HasServers()
        {
            Component.DBManager db = new Component.DBManager(db_location);

            bool ContainsServers = db.GetDocumentCount(SERVER_INFO_COLLECTION) > 0;
            
            db.Destory();
            
            db = null;

            return ContainsServers;
        }
        
        public Component.Struct.ServerCardInfo[] GetServers()
        {
            Component.DBManager db = new Component.DBManager(db_location);

            Component.Struct.ServerCardInfo[] server_info = db.GetCurrentServers(SERVER_INFO_COLLECTION, SERVER_ALIAS_COLLECTION, 20);

            db.Destory();

            db = null;

            return server_info;
        }
       
        public string GetGameByAppId(string id)
        {
            string file = string.Empty, output = string.Empty;
            JObject jsonObject;
            JToken GameEntity;

            try
            {
                file = System.Text.Encoding.Default.GetString(SteamCMDLauncher.Properties.Resources.dedicated_server_list);
                jsonObject = Newtonsoft.Json.Linq.JObject.Parse(file);
                
                GameEntity = jsonObject["server"]
                 .Children()
                 .FirstOrDefault(x => x["id"].ToString() == id);

                output = GameEntity is null ? string.Empty : GameEntity["game"].ToString();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            finally
            {
                file = null;
                jsonObject = null;
                GameEntity = null;
                id = null;
            }

            return output;
        }

        public Dictionary<string, int> GetSupportedGames()
        {
            string file;

            string key_id = "id";
            string key_game = "game";
            string key_svr = "server";

            JObject objectA;
            JToken[] objects;

            Dictionary<string, int> output = new Dictionary<string, int>();

            try
            {
                file = System.Text.Encoding.Default.GetString(Properties.Resources.dedicated_server_list);

                objectA = JObject.Parse(file);

                objects = objectA[key_svr].Children().ToArray();

                int size = objects.Length;

                for (int i = 0; i < size; i++) 
                { 
                    output.Add(objects[i].Value<string>(key_game), objects[i].Value<int>(key_id));
                }
            }
            finally
            {
                file = null;
                objectA = null;
                objects = null;

                key_game = null;
                key_svr = null;
                key_svr = null;
            }

            return output;
        }

        public bool ChangeServerAlias(string id, string new_alias)
        {
            Component.DBManager db = new Component.DBManager(db_location);

            BsonDocument document = new BsonDocument { ["_id"] = id, ["alias"] = new_alias };
            BsonDocument document_old = null;

            int operation = db.UpdateOrInsert(SERVER_ALIAS_COLLECTION, id, document, null, out document_old);

            bool result = operation > 0;

            // If the operation was to update to document instead of inserting it...
            if(operation == 2)
            {
                AddLog(id, LogType.AliasChange, $"From: '{document_old?["alias"]}', To: '{new_alias}'");
            }

            db.Destory();
            
            db = null;
            id = null;
            new_alias = null;
            document = null;
            document_old = null;

            return result;
        }

        public bool ChangeServerFolder(string id, string old_location, string new_location)
        {
            Component.DBManager db = new Component.DBManager(db_location);

            BsonDocument document = new BsonDocument { ["_id"] = id, ["folder"] = new_location };
            BsonDocument document_old = null;

            int operation = db.UpdateOrInsert(SERVER_INFO_COLLECTION, id, document, Query.EQ("folder", old_location), out document_old);

            bool result = operation > 0;

            // If the operation was to update to document instead of inserting it...
            if (operation == 2)
            {
                AddLog(id, LogType.FolderChange, $"Changed to: '{new_location}'");
            }

            db.Destory();
            
            db = null;
            id = null;
            old_location = null;
            new_location = null;
            document = null;
            document_old = null;

            return result;
        }

        public static bool CleanLog()
        {
            Log("Clearing log table");

            Component.DBManager db = new Component.DBManager(db_location);

            bool result = db.ClearTable(LOG_COLLECTION);

            if (!result) { Log($"Had problems trying to clear '{LOG_COLLECTION}' or its already empty!"); }

            db.Destory();

            db = null;

            return result;
        }

        public void RunLogQueue(BsonDocument[] elem)
        {
            if (elem is null) return;

            Component.DBManager db = new Component.DBManager(db_location);

            if (!db.InsertBulk(LOG_COLLECTION, elem, "Id")) throw new Exception(db.Reason);

            db.Destory();

            db = null;
        }
    
        public void AddLog(string id, LogType lType, string details)
        {
            if (LogQueue is null)
            {
                LogQueue = new Component.AutoFlushQueue<BsonDocument>(4, QueueRunner_Wait)
                {
                    OnFlushElapsed = RunLogQueue
                };
            }

            LogQueue.Add(new BsonDocument
            {
                ["Id"] = GetID(),
                ["svr_id"] = id,
                ["time"] = DateTime.Now.UTC_String(),
                ["type"] = ((int)lType),
                ["info"] = details
            });
        }

        public string[] FindGameID(string path)
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

        public void SetCurrentBuildVersion(string server_id, string local_build, string server_build)
        {
            Component.DBManager db = null;
            BsonDocument document;
            BsonDocument document_old = null;

            try
            {
                db = new Component.DBManager(db_location); 

                document = new BsonDocument { ["_id"] = server_id, ["local"] = local_build, ["server"] = server_build, ["lease"] = $"{DateTime.UtcNow.AddMinutes(30).ToBinary()}" };

                int operation = db.UpdateOrInsert(SERVER_BUILDV_COLLECTION, server_id, document, null, out document_old);

                bool result = operation > 0;

                // If the operation was to update to document instead of inserting it...
                if (operation == 2)
                {
                    if(local_build == server_build)
                        AddLog(server_id, LogType.ServerUpdated, "Server is now update to date with latest version");
                    else
                        AddLog(server_id, LogType.ServerOutOfDate, $"Server is out of date. Latest: {server_build}, Current: {local_build}");
                }
            }
            finally
            {
                db.Destory();
                document = null;
                db = null;
                document_old = null;
                
                server_id = null;
                local_build = null;
                server_build = null;
            }         
        }

        /// <summary>
        /// Returns a value (int) if it requires an update
        /// </summary>
        /// <param name="server_id"></param>
        /// <returns></returns>
        public int RequireUpdate(string server_id)
        {
            Component.DBManager db = null;
            BsonDocument document;
            WeakReference doc;

            try
            {
                db = new Component.DBManager(db_location);

                doc = db.FilterRowByColumnSingle(SERVER_BUILDV_COLLECTION, server_id);

                // If target is empty or nothing, return -1 as we yet to insert this document
                if (doc.Target is null) 
                {
                    Config.Log("[CFG-RU] Forcing update as server target was not found");
                    return -1; 
                }
                
                // If not, this means an entity has been found
                document = doc.Target as BsonDocument;

                // First validate the lease...

                // If the cast more above fails, perform an update to correct it again
                if(document["lease"].RawValue is null)
                {
                    Config.Log("[CFG-RU] Forcing update as column \"lease\" has problems parsing or doesn't exist");
                    return -1;
                }

                // Get the date it was last amended
                DateTime parsedLease = DateTime.FromBinary(Convert.ToInt64(document["lease"].AsDouble));

                // Compare date first (If the current date is more, perform update)
                if (parsedLease < DateTime.Now)
                {
                    Config.Log("[CFG-RU] Forcing update as lease is non-effective");
                    return -1;
                }

                // If we get here, it means the lease is still in place!
                return !document["server"].Equals(document["local"]) ? 1 : 0;
            }
            finally
            {
                db.Destory();
                document = null;
                db = null;
                server_id = null;
                doc = null;
            }
        }

        #endregion

        #region Utilities
        public static string GetFolder(string required_file, string rule_break)
        {
            Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog
            {
                InitialDirectory = "C:\\Users",
                IsFolderPicker = true
            };

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
            Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog {
                InitialDirectory = "C:\\Users",
                IsFolderPicker = true
            };

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
        /// <param name="relative_path">The folder to access from the exe path directly</param>
        /// <returns>Absolute Path to the file, null if cancelled</returns>
        public static string GetFile(string target_type, string relative_path = "configs")
        {
            Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog();

            string target_dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relative_path);

            if (!Directory.Exists(target_dir))
                Directory.CreateDirectory(target_dir);

            dialog.InitialDirectory = target_dir;

            dialog.Title = "Select the file you need";

            dialog.Filters.Add(new Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogFilter($"{target_type[1..].ToUpper()} Files", $"*{target_type}"));
            
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

            relative_path = null;
            target_type = null;
            dialog = null;
            return string.Empty;
        }

        public static void Log(string text, bool play_beep_sound = false)
        {
            #if RELEASE
            if(string.IsNullOrEmpty(SessionFileName))
            {
                string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

                if (!Directory.Exists(logFolder))
                    Directory.CreateDirectory(logFolder);

                string[] currentFiles = Directory.GetFiles(logFolder);
                
                int latestFile = 0;
                if (currentFiles.Length > 0)
                    latestFile = Convert.ToInt32(Path.GetFileNameWithoutExtension(currentFiles[^1])[14..]);
                  
                SessionFileName = Path.Combine(logFolder, $"scmdl_session_{++latestFile}.txt");
                
                currentFiles = null;
                
                logFolder = null;
            }

            using (StreamWriter sw = File.AppendText(SessionFileName))
            {
                DateTime now = DateTime.Now;
                sw.WriteLine($"[{now.ToString("dd/MM/yyyy HH:mm:ss.fff")}] : {text}");
            }
            #else
                System.Diagnostics.Debug.WriteLine(text);
            #endif

            if(play_beep_sound) System.Media.SystemSounds.Beep.Play();
        }

        public static bool GetEmbededResource(string file, out string[] output)
        {
            System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
            StreamReader rs;

            string config_f = null;
            
            try
            {
                rs = new StreamReader(asm.GetManifestResourceStream($"SteamCMDLauncher.Resources.{file}"));

                config_f = rs.ReadToEnd();

                return true;
            }
            catch (Exception ex)
            {
                Config.Log($"[GER] [!] EMBEDED ERROR - NO INTERNAL FILE OF '{file}' WAS FOUND [!]");
                Config.Log($"[GER] REASON: {ex.Message}");
            }
            finally
            {
                output = config_f?.Split('\n');
                
                config_f = null;
                file = null;
                rs = null;
                asm = null;
            }

            return false;
        }

        /// <summary>
        /// Returns the SHA-256 of a file, assuming 'path' is an actual file
        /// </summary>
        /// <param name="path">The file to get the SHA-256 hash from</param>
        /// <returns>The 65 length hash SHA-256 of the file - 'string.Empty' if not a file</returns>
        public static string GetSHA256Sum(string path)
        {
            Config.Log($"[SHA-256] Getting hash from file: {path}");
            if (!System.IO.File.Exists(path)) return string.Empty;

            // https://stackoverflow.com/questions/38474362/get-a-file-sha256-hash-code-and-checksum/51966515#51966515
            // Same idea but different logic (this comment means to only output the B64 string, not the hex we need!)

            // Good resource: https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.hashalgorithm.computehash?view=net-5.0

            string output = null;

            using (System.Security.Cryptography.SHA256 hash = System.Security.Cryptography.SHA256Managed.Create())
            {
                byte[] encr = hash.ComputeHash(System.IO.File.ReadAllBytes(path));

                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                System.Collections.IEnumerator bEnum = encr.GetEnumerator();

                string hex_cvrt = "x2";

                while (bEnum.MoveNext())
                {
                    sb.Append(((byte)bEnum.Current).ToString(hex_cvrt));
                }

                output = sb.ToString();
                hex_cvrt = null;
                sb = null;
                bEnum = null;
                encr = null;
            }

            return output;
        }
        #endregion
    }
}