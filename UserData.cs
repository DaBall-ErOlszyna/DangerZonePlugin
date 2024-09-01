//#define USERDATA

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Entities;
#if USERDATA
using MySql.Data;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI;
#endif

namespace DangerZonePlugin
{
    public class UserData
    {
        public struct User
        {
            public long SteamID, Wins, Loses, Kills;
        }

        private const string Server = "localhost";
        private const string DatabaseName = "dangerzone";
        private const string Username = "root";
        private const string Password = "";

#if USERDATA
        public MySqlConnection Connection { get; set; }
#endif

        private static UserData _instance = null;
        public static UserData Instance()
        {
            if (_instance == null)
                _instance = new UserData();
            return _instance;
        }

        public bool IsConnect()
        {
#if USERDATA
            if (Connection == null)
            {
                if (String.IsNullOrEmpty(DatabaseName))
                    return false;
                string connstring = string.Format("Server={0}; database={1}; UID={2}; password={3}", Server, DatabaseName, Username, Password);
                Connection = new MySqlConnection(connstring);
                Connection.Open();
            }

#endif
            return true;
        }

        public User? GetUser(long SteamID, string name)
        {
#if USERDATA
            if (IsConnect())
            {
                MySqlCommand myCommand = new MySqlCommand();
                myCommand.Connection = Connection;
                myCommand.CommandText = @"SELECT * FROM `users` WHERE steamid = '" + SteamID + "';";

                // execute the command and read the results
                using var myReader = myCommand.ExecuteReader();
                while (myReader.Read())
                {
                    var kills = myReader.GetInt64("kills");
                    var deaths = myReader.GetInt64("deaths");
                    var wins = myReader.GetInt64("wins");

                    User userfound = new User() { SteamID = SteamID, Kills = kills, Loses = deaths, Wins = wins };

                    myReader.Close();
                    return userfound;

                }
                myReader.Close();

                // no users found
                User user = new User() { SteamID = SteamID, Kills = 0, Loses = 0, Wins = 0 };

                MySqlCommand commandAdd = new MySqlCommand();
                commandAdd.Connection = Connection;
                commandAdd.CommandText = @"INSERT INTO `users`(`steamid`, `name`) VALUES ('" + SteamID + "','" + MySqlHelper.EscapeString(name) + "')";

                commandAdd.ExecuteNonQuery();

                return user;

            }
#endif
            return null;

        }

        public void UpdateUser(User user)
        {
#if USERDATA
            if (IsConnect())
            {
                MySqlCommand myCommand = new MySqlCommand();
                myCommand.Connection = Connection;
                myCommand.CommandText = @"UPDATE `users` SET `kills`='" + user.Kills + "',`deaths`='" + user.Loses + "',`wins`='" + user.Wins + "' WHERE `steamid`='" + user.SteamID + "'";

                // execute the command and read the results
                myCommand.ExecuteNonQuery();

            }
#endif
        }

        public void Close()
        {
#if USERDATA
            Connection.Close();
#endif
        }


    }

    public static class UserConsts
    {
        public const int MINIMUM_RATING = 1;
        public const int MAXIMUM_RATING = 10000;
        public static long GetRating(long kills, long deaths, long wins)
        {
            return (long) Math.Ceiling((decimal)((wins+1)/(deaths+1)+kills)*10)*10;
        }
    }
}
