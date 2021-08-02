using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiteDB;

namespace SteamCMDLauncher
{
    public static class Config
    {
        #region Variables/Constants
        private static readonly string db_location = System.IO.Path.Combine(Environment.CurrentDirectory, "data.db");

        private static string db_error = string.Empty;

        public const string INFO_COLLECTION = "ifo";
        public const string SERVER_COLLECTION = "sc";
        public const string LOG_COLLECTION = "lg";
        public const string SERVER_INFO_COLLECTION = "sci";
        public const string SERVER_ALIAS_COLLECTION = "sca";
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
                   
                    col.Insert(new BsonDocument { ["_id"] = GetID(), ["app_id"] = id, ["folder"]=folder_loc});

                    if (!Require_Get_Server)
                        Require_Get_Server = true;

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

                    // Perform "INSERT" query if doesn't exist, else perform "UPDATE" query
                    if (r is null)
                        col.Insert(new BsonDocument { ["_id"] = id, ["alias"] = new_alias });
                    else
                    {
                        r["alias"] = new_alias;
                        col.Update(r);
                    }

                    return !(r is null);
                }
            }
            catch (Exception _)
            {
                db_error = _.Message;
            }

            return false;
        }

        public static bool ChangeServerFolder(string id, string new_location)
        {
            db_error = string.Empty;

            ILiteCollection<BsonDocument> col;

            try
            {
                using (var db = new LiteDatabase(db_location))
                {
                    col = db.GetCollection(INFO_COLLECTION);

                    var r = col.FindById(id);

                    // Perform "INSERT" query if doesn't exist, else perform "UPDATE" query
                    if (r is null)
                        col.Insert(new BsonDocument { ["_id"] = id, ["svr"] = new_location });
                    else
                    {
                        r["svr"] = new_location;
                        col.Update(r);
                    }

                    return !(r is null);
                }
            }
            catch (Exception _)
            {
                db_error = _.Message;
            }

            return false;
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

        public static void Log(string text) => System.Diagnostics.Debug.WriteLine(text);
        #endregion
    }
}