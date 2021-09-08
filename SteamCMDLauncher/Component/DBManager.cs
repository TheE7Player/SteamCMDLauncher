using LiteDB;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

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

        /// <summary>
        /// Creates the DB instance and connects to it
        /// </summary>
        /// <param name="path">The path to the database file (*.db)</param>
        public DBManager(string path)
        {
            db = new LiteDatabase(path);
            disposed = false;
            Reason = string.Empty;
        }

        /// <summary>
        /// Frees up from reference objects (if any)
        /// </summary>
        public void Destory()
        {
            if (!disposed)
            {
                db.Dispose();
                db = null;
                disposed = true;
                Reason = null;
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
                System.Threading.Monitor.Enter(db_lock);
                _table = GetTable(table);
                
                if (_table != null) 
                {
                    BsonValue result = ((ILiteCollection<BsonDocument>)_table.Target).Insert(document);
            
                    if (!result.IsObjectId) { Reason = $"Document insert returned false: {document}"; } else { r = true; }
                
                    result = null;
                }
            }
            finally
            {   
                table = null;
                document = null;
                _table = null;
                System.Threading.Monitor.Exit(db_lock);
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
            System.Threading.Monitor.Enter(db_lock);

            WeakReference _table = GetTable(table);
            
            bool r = false;

            if (_table != null)
            {
                int bulk_size = documents.Length;
            
                BsonValue result;
            
                for (int i = 0; i < bulk_size; i++)
                {
                    result = ((ILiteCollection<BsonDocument>)_table.Target).Insert(documents[i]);

                    if (!result.IsObjectId) { Reason = $"Element {i} returned false: {documents[i]}"; break; }
                }

                if(r) ((ILiteCollection<BsonDocument>)_table.Target).EnsureIndex(ensure_index);
                
                result = null;

                r = true;
            }
            
            documents = null;           
            ensure_index = null;
            _table = null;
            table = null;

            System.Threading.Monitor.Exit(db_lock);

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
                System.Threading.Monitor.Enter(db_lock);
                
                _table = GetTable(table);

                r = ((ILiteCollection<BsonDocument>)_table.Target).DeleteAll() > 0;
            }
            finally
            {   
                table = null;
                _table = null;
                System.Threading.Monitor.Exit(db_lock);
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
                System.Threading.Monitor.Enter(db_lock);
                
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
                System.Threading.Monitor.Exit(db_lock);
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
                System.Threading.Monitor.Enter(db_lock);
                _table = GetTable(table);
                count = ((ILiteCollection<BsonDocument>)_table.Target).FindAll().Count();
            }
            finally
            {
                _table = null;
                table = null;
                System.Threading.Monitor.Exit(db_lock);
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
                System.Threading.Monitor.Enter(db_lock);

                _table = GetTable(target);

                if(((ILiteCollection<BsonDocument>)_table.Target).DeleteMany(x => x[target_key] == document_id) == 0)
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
                System.Threading.Monitor.Exit(db_lock);
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
                System.Threading.Monitor.Enter(db_lock);

                _table = GetTable(table);

                old_document = custom_query == null ? ((ILiteCollection<BsonDocument>)_table.Target).FindOne(id) : ((ILiteCollection<BsonDocument>)_table.Target).FindOne(custom_query);

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
                System.Threading.Monitor.Exit(db_lock);
            }

            return result;
        }

        public Component.Struct.ServerCardInfo[] GetCurrentServers(string server_table, string alias_table, int size)
        {
            WeakReference table_server, table_alias, server_enum, alias_search;
            Component.Struct.ServerCardInfo[] output = null;
            List<Component.Struct.ServerCardInfo> server_list;
            int fixed_size = 0;

            try
            {
                System.Threading.Monitor.Enter(db_lock);
                
                table_server = GetTable(server_table);
                table_alias = GetTable(server_table);

                server_list = new List<Struct.ServerCardInfo>(size);

                server_enum = new WeakReference(((ILiteCollection<BsonDocument>)table_server.Target).FindAll());

                if(server_enum != null)
                {
                    BsonDocument[] server_size = ((IEnumerable<BsonDocument>)(server_enum.Target)).ToArray();
                    fixed_size = server_size.Length;

                    server_list = new List<Struct.ServerCardInfo>(fixed_size);

                    for (int i = 0; i < fixed_size; i++)
                    {
                        alias_search = new WeakReference(((ILiteCollection<BsonDocument>)table_alias.Target).FindOne(Query.EQ("_id", server_size[i]["_id"])));

                        server_list.Add(new Component.Struct.ServerCardInfo()
                        {
                            Unique_ID = server_size[i]["_id"],
                            GameID = server_size[i]["app_id"].RawValue.ToString(),
                            Folder = server_size[i]["folder"],
                            Alias = alias_search is null ? string.Empty : ((BsonDocument)(alias_search.Target))["alias"].AsString
                        });
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
                System.Threading.Monitor.Exit(db_lock);
            }

            return output;
        }
    }
}
