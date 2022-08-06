using MySql.Data.MySqlClient;
using Mono.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using TShockAPI;
using TShockAPI.DB;

namespace KillStats
{
    public class DB
    {
        private IDbConnection db;

        public List<KSUser> Users = new List<KSUser>();

        public DB()
        {
            switch (TShock.Config.Settings.StorageType.ToLower())
            {
                case "mysql":
                    string[] host = TShock.Config.Settings.MySqlHost.Split(':');
                    db = new MySqlConnection()
                    {
                        ConnectionString = String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                        host[0],
                        host.Length == 1 ? "3306" : host[1],
                        TShock.Config.Settings.MySqlDbName,
                        TShock.Config.Settings.MySqlUsername,
                        TShock.Config.Settings.MySqlPassword)
                    };
                    break;
                case "sqlite":
                    string dbPath = Path.Combine(TShock.SavePath, "KillStats.sqlite");
                    db = new SqliteConnection(String.Format("uri=file://{0},Version=3", dbPath));
                    break;
            }

            SqlTableCreator creator = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite ?
                    (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            creator.EnsureTableStructure(new SqlTable("KillStats",
                new SqlColumn("ID", MySqlDbType.Int32) { AutoIncrement = true, Primary = true },
                new SqlColumn("UserID", MySqlDbType.Int32),
                new SqlColumn("Name", MySqlDbType.Text),
                new SqlColumn("Deaths", MySqlDbType.Int32),
                new SqlColumn("PvPKills", MySqlDbType.Int32)));

            using (QueryResult result = db.QueryReader("SELECT * FROM KillStats"))
            {
                while (result.Read())
                {
                    Users.Add(new KSUser(
                        result.Get<string>("Name"),
                        result.Get<int>("UserID"),
                        result.Get<int>("Deaths"),
                        result.Get<int>("PvPKills")
                        ));
                }
            }
        }

        public void add(KSUser user, TSPlayer ply)
        {
            try
            {
                bool exists = false;
                foreach (KSUser usr in Users)
                {
                    if (usr.UserID == user.UserID)
                    {
                        exists = true;
                    }
                }

                if (exists == false)
                {
                    db.Query("INSERT INTO KillStats (UserID, Name, Deaths, PvPKills) VALUES (@0, @1, @2, @3)",
                    user.UserID,
                    user.Name,
                    user.Deaths,
                    user.PvPKills
                    );
                    Users.Add(user);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public void update(KSUser user)
        {
            foreach (KSUser usr in Users)
            {
                if (usr.UserID == user.UserID)
                {
                    try
                    {
                        db.Query("UPDATE KillStats SET Deaths=@0, PvPKills=@1 WHERE UserID=@2",
                        user.Deaths,
                        user.PvPKills,
                        user.UserID
                        );
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                    }

                }
            }
        }
    }
}
