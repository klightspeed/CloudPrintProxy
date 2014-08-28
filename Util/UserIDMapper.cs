using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;

namespace TSVCEO.CloudPrint.Util
{
    public static class UserIDMapper
    {
        #region private fields / properties

        private static object UserIdCacheLock;
        private static Dictionary<string, string> UserIdCache { get; set; }
        private static SQLiteConnection UserIDMappingDatabase { get; set; }

        #endregion

        #region constructor

        static UserIDMapper()
        {
            UserIdCacheLock = new object();
            UserIdCache = new Dictionary<string, string>();
            UserIDMappingDatabase = new SQLiteConnection("Data Source=" + Config.UserIDMappingDatabaseFilename);
            UserIDMappingDatabase.Open();
            InitUsersTable();
        }

        #endregion

        #region private methods

        private static void InitUsersTable()
        {
            using (var cmd = UserIDMappingDatabase.CreateCommand())
            {
                cmd.CommandText = "CREATE TABLE IF NOT EXISTS users (username TEXT PRIMARY KEY, userid TEXT)";
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region public methods / properties

        public static Dictionary<string, string> GetAllUserIds()
        {
            Dictionary<string, string> userids = new Dictionary<string, string>();

            using (var cmd = UserIDMappingDatabase.CreateCommand())
            {
                cmd.CommandText = "SELECT username, userid FROM users";
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        userids[rdr["username"].ToString()] = rdr["userid"].ToString();
                    }
                }
            }

            lock (UserIdCacheLock)
            {
                UserIdCache = userids;
            }

            return userids;
        }

        public static string GetUserId(string username)
        {
            lock (UserIdCacheLock)
            {
                if (!UserIdCache.ContainsKey(username))
                {
                    using (var cmd = UserIDMappingDatabase.CreateCommand())
                    {
                        cmd.CommandText = "SELECT userid FROM users WHERE username = @username";
                        cmd.Parameters.AddWithValue("@username", username);
                        var result = cmd.ExecuteScalar();

                        if (result != null)
                        {
                            UserIdCache[username] = result.ToString();
                        }
                    }
                }

                return UserIdCache.ContainsKey(username) ? UserIdCache[username] : null;
            }
        }

        public static void CreateUser(string username, string userid)
        {
            lock (UserIdCacheLock)
            {
                UserIdCache[username] = userid;

                using (var cmd = UserIDMappingDatabase.CreateCommand())
                {
                    cmd.CommandText = "INSERT OR REPLACE INTO users (username, userid) VALUES (@username, @userid)";
                    cmd.Parameters.AddWithValue("@username", username);
                    cmd.Parameters.AddWithValue("@userid", userid);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void DeleteUser(string username)
        {
            lock (UserIdCacheLock)
            {
                if (UserIdCache.ContainsKey(username))
                {
                    UserIdCache.Remove(username);
                }

                using (var cmd = UserIDMappingDatabase.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM users WHERE username = @username";
                    cmd.Parameters.AddWithValue("@username", username);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        #endregion
    }
}
