using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;

namespace TSVCEO.CloudPrint.InfoServer
{
    public class Session
    {
        protected static SQLiteConnection SessionDatabase { get; set; }

        static Session()
        {
            SessionDatabase = new SQLiteConnection("Data Source=" + Config.SessionDatabaseFilename);
            SessionDatabase.Open();
            InitSessionsTable();
        }

        public Session(string sessionid)
        {
            SessionID = sessionid;
        }

        protected static void InitSessionsTable()
        {
            var cmd = SessionDatabase.CreateCommand();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS sessions (sessionid TEXT, name TEXT, value TEXT, PRIMARY KEY(sessionid, name))";
            cmd.ExecuteNonQuery();
        }

        public string SessionID { get; protected set; }

        public void Delete()
        {
            var cmd = SessionDatabase.CreateCommand();
            cmd.CommandText = "DELETE FROM sessions WHERE sessionid = @sessionid";
            cmd.Parameters.AddWithValue("@sessionid", SessionID);
            cmd.ExecuteNonQuery();
        }

        public void Delete(string name)
        {
            var cmd = SessionDatabase.CreateCommand();
            cmd.CommandText = "DELETE FROM sessions WHERE sessionid = @sessionid AND name = @name";
            cmd.Parameters.AddWithValue("@sessionid", SessionID);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.ExecuteNonQuery();
        }

        public void Set(string name, string value)
        {
            if (value == null)
            {
                Delete(name);
            }
            else
            {
                var cmd = SessionDatabase.CreateCommand();
                cmd.CommandText = "INSERT OR REPLACE INTO sessions (sessionid, name, value) VALUES (@sessionid, @name, @value)";
                cmd.Parameters.AddWithValue("@sessionid", SessionID);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@value", value);
                cmd.ExecuteNonQuery();
            }
        }

        public string Get(string name)
        {
            var cmd = SessionDatabase.CreateCommand();
            cmd.CommandText = "SELECT value FROM sessions WHERE sessionid = @sessionid AND name = @name";
            cmd.Parameters.AddWithValue("@sessionid", SessionID);
            cmd.Parameters.AddWithValue("@name", name);
            var result = cmd.ExecuteScalar();
            return result == null ? null : result.ToString();
        }

        public string this[string name]
        {
            get
            {
                return Get(name);
            }
            set
            {
                Set(name, value);
            }
        }

    }
}
