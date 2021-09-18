using LiteDB;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Collections.Concurrent;
using System.IO;

namespace SteamCMDLauncher.Component
{
    public class DBManager
    {
        private LiteDatabase db;
        private bool disposed;
        private static readonly object db_lock = new object();

        /// <summary>
        /// States the reason why it failed (if any)
        /// </summary>
        public string Reason { get; private set; }

        private void StartLock(string message, bool constructor = false)
        {       
            System.Threading.Monitor.Enter(db_lock);

            Config.Log($"[DBM] {message}");

            message = null;
        }

        private void EndLock(string message, bool constructor = false)
        {
            System.Threading.Monitor.Exit(db_lock);
            
            Config.Log($"[DBM] {message}");
            
            message = null;
        }

        /// <summary>
        /// Creates the DB instance and connects to it
        /// </summary>
        /// <param name="path">The path to the database file (*.db)</param>
        public DBManager(string path)
        {
            try
            {
                StartLock("Creating DBManager instance", true);
                db = new LiteDatabase(path);
                disposed = false;
                Reason = string.Empty;
            }
            catch (Exception ex)
            {
                Config.Log($"[DBM] DBManager Cont error: {ex.Message}");
                throw new Exception("Database read failed, database is being used currently or doesn't exist");
            }
            finally
            {
                EndLock("Finished DBManager instance", true);
            }
        }

        /// <summary>
        /// Frees up from reference objects (if any)
        /// </summary>
        public void Destory()
        {
            Config.Log("[DBM] Starting .Destory()");
            if (!disposed)
            {
                db.Dispose();
                db = null;
                disposed = true;
                Reason = null;
                Config.Log("[DBM] Manager has been disposed of successfully");
            } 
            else
            {
                Config.Log("[DBM] Manager has already been disposed of, this shouldn't have been called or accessed!");
            }
        }

        private WeakReference GetTable(string table) => new WeakReference(db.GetCollection(table));

        /// <summary>
        /// Inserts a single document into the database
        /// </summary>
        /// <param name="table">The table to insert the data into</param>
        /// <param name="document">The information to insert</param>
        /// <returns></returns>
        public bool Insert(string table, BsonDocument document)
        {
            WeakReference _table;
            
            bool r = false;
                      
            try
            {
                StartLock("Entering Insert");

                _table = GetTable(table);
                
                if (_table != null) 
                {
                    BsonValue result = ((ILiteCollection<BsonDocument>)_table.Target).Insert(document);
            
                    if (result.IsNull) { Reason = $"Document insert returned false: {document}"; } else { r = true; }
                
                    result = null;
                }
            }
            finally
            {   
                table = null;
                document = null;
                _table = null;
                EndLock("Exiting Insert");
            }
            
            return r;
        }

        /// <summary>
        /// Inserts an array of objects, must include ensure index
        /// </summary>
        /// <param name="table">The table to insert multiple documents to</param>
        /// <param name="documents">The array of documents to insert</param>
        /// <param name="ensure_index">The column to ensure that an index is in place (hashed)</param>
        /// <returns></returns>
        public bool InsertBulk(string table, BsonDocument[] documents, string ensure_index)
        {                   
            WeakReference _table = GetTable(table);
            
            bool r = false;

            try
            {
                StartLock("Entering InsertBulk");
                
                if (_table != null)
                {
                    int bulk_size = documents.Length;
            
                    BsonValue result;
            
                    for (int i = 0; i < bulk_size; i++)
                    {
                        result = ((ILiteCollection<BsonDocument>)_table.Target).Insert(documents[i]);

                        if (result.IsNull) { Reason = $"Element {i} returned false: {documents[i]}"; break; }
                    }

                    ((ILiteCollection<BsonDocument>)_table.Target).EnsureIndex(ensure_index);
                
                    result = null;

                    r = true;
                }
            }
            finally
            {
                documents = null;
                ensure_index = null;
                _table = null;
                table = null;
                EndLock("Exiting InsertBulk");
            }

            return r;
        }
    
        /// <summary>
        /// Clears a tables contents, doesn't delete the table
        /// </summary>
        /// <param name="table">The table to delete its contents (clear it)</param>
        /// <returns></returns>
        public bool ClearTable(string table)
        {
            WeakReference _table;
            bool r = false;
            
            try
            {
                StartLock("Entering ClearTable");
                
                _table = GetTable(table);

                r = ((ILiteCollection<BsonDocument>)_table.Target).DeleteAll() > 0;
            }
            finally
            {   
                table = null;
                _table = null;
                EndLock("Exiting ClearTable");
            }
            
            return r;
        }

        /// <summary>
        /// Gets a key from a table, and receives its value based on the key given
        /// </summary>
        /// <param name="table">The table to look at</param>
        /// <param name="key">The key to extract a value from</param>
        /// <returns></returns>
        public WeakReference FilterKey(string table, string key)
        {
            WeakReference _table, self;
            
            try
            {
                StartLock("Entering FilterKey");
                
                _table = GetTable(table);

                self = new WeakReference(((ILiteCollection<BsonDocument>)_table.Target)
                    .FindAll()
                    .SingleOrDefault(x => x.Keys.Contains(key)));
            }
            finally
            {
                _table = null;
                key = null;
                table = null;
                EndLock("Exiting FilterKey");
            }
            
            return self;
        }
    
        /// <summary>
        /// Returns how many elements are in a given table
        /// </summary>
        /// <param name="table">The table to get the amount of documents of</param>
        /// <returns></returns>
        public int GetDocumentCount(string table)
        {
            WeakReference _table;
            int count;
            
            try
            {
                StartLock("Entering GetDocumentCount");
                _table = GetTable(table);
                count = ((ILiteCollection<BsonDocument>)_table.Target).FindAll().Count();
            }
            finally
            {
                _table = null;
                table = null;
                EndLock("Exiting GetDocumentCount");
            }
            return count;
        }
    
        /// <summary>
        /// Removes an item (including multiple instances) of a matched criteria
        /// </summary>
        /// <param name="document_id">The ID to delete from</param>
        /// <param name="target">The table to do this operation from</param>
        /// <param name="target_key">The key to look at to delete (filter from)</param>
        /// <returns></returns>
        public bool RemoveMany(string document_id, string target, string target_key = "_id")
        {
            WeakReference _table;
            bool r = false;
            
            try
            {
                StartLock("Entering RemoveMany");

                _table = GetTable(target);

                if (((ILiteCollection<BsonDocument>)_table.Target).DeleteMany(x => x[target_key] == document_id) == 0)
                {
                    Reason = $"Cannot delete documents from table '{target}' with id: {document_id}, as they aren't any to begin with.";
                }
                else
                {
                    r = true;
                    db.Rebuild();
                }
            }
            finally
            {
                _table = null;
                document_id = null;
                target = null;
                target_key = null;
                EndLock("Exiting RemoveMany");
            }

            return r;
        }

        /// <summary>
        /// Performs an Update if the document exists already, performs insert if not
        /// </summary>
        /// <param name="table">The table to update or append to</param>
        /// <param name="id">The document ID to add or update</param>
        /// <param name="document">The document to operate with</param>
        /// <param name="custom_query">The query to use to filter (null if just ID search)</param>
        /// <param name="old_document">The old document (null if its insert operation)</param>
        /// <returns> 0 - Failed | 1 - Insert | 2 - Update </returns>
        public int UpdateOrInsert(string table, string id, BsonDocument document, BsonExpression custom_query, out BsonDocument old_document)
        {
            WeakReference _table;
            
            int result = 0;
            
            try
            {
                StartLock("Entering UpdateOrInsert");
                
                _table = GetTable(table);

                old_document = custom_query == null ? ((ILiteCollection<BsonDocument>)_table.Target).FindById(id) :
                    ((ILiteCollection<BsonDocument>)_table.Target).FindOne(custom_query);

                result = old_document == null ? 1 : 2;

                if (result == 1)
                {
                    ((ILiteCollection<BsonDocument>)_table.Target).Insert(document);
                }
                else
                {
                    ((ILiteCollection<BsonDocument>)_table.Target).Update(document);
                }
            }
            finally
            {
                _table = null;
                table = null;
                document = null;
                id = null;
                EndLock("Exiting UpdateOrInsert");
            }

            return result;
        }

        /// <summary>
        /// Gets all the current servers that are saved and gets its aliases if set also
        /// </summary>
        /// <param name="server_table">The table to grab the servers from</param>
        /// <param name="alias_table">The table that contains each servers aliases</param>
        /// <param name="size">The max size it should return</param>
        /// <returns></returns>
        public Struct.ServerCardInfo[] GetCurrentServers(string server_table, string alias_table, int size)
        {
            WeakReference table_server, table_alias, server_enum, alias_search;
            Component.Struct.ServerCardInfo[] output = null;
            List<Component.Struct.ServerCardInfo> server_list;
            int fixed_size = 0;
            
            try
            {
                StartLock("Entering GetCurrentServers");

                table_server = GetTable(server_table);
                table_alias = GetTable(alias_table);

                server_list = new List<Struct.ServerCardInfo>(size);

                server_enum = new WeakReference(((ILiteCollection<BsonDocument>)table_server.Target).FindAll());

                if(server_enum != null)
                {
                    BsonDocument[] server_size = ((IEnumerable<BsonDocument>)(server_enum.Target)).ToArray();
                    
                    fixed_size = server_size.Length;

                    server_list = new List<Struct.ServerCardInfo>(fixed_size);
                    
                    string alias;
                    
                    for (int i = 0; i < fixed_size; i++)
                    {
                        alias_search = new WeakReference(((ILiteCollection<BsonDocument>)table_alias.Target).FindOne(Query.EQ("_id", server_size[i]["_id"])));

                        alias = (BsonDocument)alias_search.Target == null ? string.Empty : ((BsonDocument)alias_search?.Target)["alias"].AsString;

                        server_list.Add(new Component.Struct.ServerCardInfo()
                        {
                            Unique_ID = server_size[i]["_id"],
                            GameID = server_size[i]["app_id"].RawValue.ToString(),
                            Folder = server_size[i]["folder"],
                            Alias = alias
                        });

                        alias = null;
                    }

                    server_size = null;

                    output = server_list.ToArray();
                }
            }
            finally
            {
                server_table = null;
                alias_table = null;
                table_server = null;
                table_alias = null;
                server_list = null;
                server_enum = null;
                alias_search = null; 
                EndLock("Exiting GetCurrentServers");
            }

            return output;
        }
    
        /// <summary>
        /// Gets the information from the servers based on the ID given
        /// </summary>
        /// <param name="table">The table which contains the server logs of each action</param>
        /// <param name="id">The server id to look for</param>
        public Component.Struct.ServerLog ServerDetails(string table, string id)
        {
            WeakReference table_server, table_documents;

            Struct.ServerLog result = new Struct.ServerLog(0);
         
            try
            {
                StartLock("Entering ServerDetails");

                table_server = GetTable(table);

                table_documents = new WeakReference(((ILiteCollection<BsonDocument>)table_server.Target).FindAll());

                if (table_documents != null)
                {
                    string id_find = "svr_id";
                    
                    IEnumerable<BsonDocument> details = ((IEnumerable<BsonDocument>)table_documents.Target).Where(x => x[id_find].Equals(id));

                    int length = details.Count();

                    if(length > 0)
                    {

                        result.ResizeNew(length);

                        string colType = "type";
                        string colDetail = "info";
                        string colUTC = "time";

                        foreach (BsonDocument item in details)
                        {
                            result.Add(item[colType], item[colDetail], item[colUTC]);
                        }

                        colType = null;
                        colDetail = null;
                        colUTC = null;
                    }

                    details = null;
                    id_find = null;
                }
            }
            finally
            {
                table_server = null;
                table_documents = null;
                table = null;
                id = null;
                EndLock("Exiting ServerDetails");
            }

            return result;
        }
    }
}
