using System.Collections.Generic;
using TShockAPI;

namespace KillStats
{
    public class KSUser
    {
        public string Name { get; set; }
        public int UserID { get; set; }
        public int Deaths { get; set; }
        public int PvPKills { get; set; }
        public TSPlayer Killer { get; set; }

        public KSUser(string name, int userid, int deaths, int pvpkills)
        {
            Name = name;
            UserID = userid;
            Deaths = deaths;
            PvPKills = pvpkills;
        }
    }
}
