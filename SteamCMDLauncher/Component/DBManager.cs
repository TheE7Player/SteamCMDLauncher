using LiteDB;
using System;
using System.Collections.Generic;
using System.Text;

namespace SteamCMDLauncher.Component
{
    public class DBManager
    {
        private LiteDatabase db;
        private bool disposed;
        private static readonly object db_lock = new object();

        /// <summary>
        /// Creates the DB instance and connects to it
        /// </summary>
        /// <param name="path">The path to the database file (*.db)</param>
        public DBManager(string path)
        {
            db = new LiteDatabase(path);
            disposed = false;
        }

        public void Destory()
        {
            if (!disposed)
            {
                db.Dispose();
                db = null;
                disposed = true;
            }
        }

        private WeakReference GetTable(string table)
        {
            return new WeakReference(db.GetCollection(table));
        }

        public bool Insert(string table, BsonDocument document)
        {
            System.Threading.Monitor.Enter(db_lock);
           
            WeakReference _table = GetTable(table);

            if (_table is null) 
            {
                System.Threading.Monitor.Exit(db_lock);
                return false; 
            }

            BsonValue result = ((ILiteCollection<BsonDocument>)_table.Target).Insert(document);

            table = null;
            document = null;

            bool r = result.IsObjectId;
            result = null;
            
            System.Threading.Monitor.Exit(db_lock);
            
            return r;
        }
    }
}
