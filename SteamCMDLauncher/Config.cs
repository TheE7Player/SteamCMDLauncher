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
        private static readonly string db_location = System.IO.Path.Combine(Environment.CurrentDirectory, "data.db");

        private static string db_error = string.Empty;

        public const string INFO_COLLECTION = "ifo";
        public const string SERVER_COLLECTION = "sc";
        public const string LOG_COLLECTION = "lg";
        public const string SERVER_INFO_COLLECTION = "sci";

        public static bool DatabaseExists => System.IO.File.Exists(db_location);

        public static string DatabaseLocation => db_location;

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

                    var duplicate = current_table.FirstOrDefault(x => x.Keys.Contains(key));
                    
                    if (!ReferenceEquals(duplicate, null))
                    {
                        duplicate[key] = new BsonValue(value);
                        col.Update(duplicate);
                    }
                    else
                        col.Insert(new BsonDocument { ["_id"] = ObjectId.NewObjectId(), [key] = new BsonValue(value) });
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

                    col.Insert(new BsonDocument { ["_id"] = ObjectId.NewObjectId(), ["app_id"] = id, ["folder"]=folder_loc });

                    return true;
                }
            }
            catch (Exception _)
            {
                db_error = _.Message;
            }

            return false;
        }
    }
}
