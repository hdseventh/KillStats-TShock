using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace KillStats
{
    [ApiVersion(2, 1)]
    public class KillStats : TerrariaPlugin
    {
        public override string Author => "hdseventh";
        public override string Description => "PVP KillStats for TShock";
        public override string Name => "KillStats";
        public override Version Version { get { return new Version(1, 6, 0, 0); } }
        public DB KSSystem { get; set; }
        public KillStats(Main game) : base(game) { }


        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.NetGetData.Register(this, onGetData);
            ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
            TShockAPI.Hooks.PlayerHooks.PlayerPostLogin += OnPlayerLogin;
            GetDataHandlers.InitGetDataHandler();
        }

        public void OnInitialize(EventArgs args)
        {
            Commands.ChatCommands.Add(new Command(getStats, "ckills"));
            Commands.ChatCommands.Add(new Command(getRanks, "cranks"));
        }

        private void OnPostInitialize(EventArgs args)
        {
            KSSystem = new DB();
        }

        private void onGetData(GetDataEventArgs e)
        {
            PacketTypes type = e.MsgID;
            var player = TShock.Players[e.Msg.whoAmI];
            if (player == null || !player.ConnectionAlive)
            {
                e.Handled = true;
                return;
            }

            using (var data = new MemoryStream(e.Msg.readBuffer, e.Index, e.Length))
            {
                try
                {
                    if (GetDataHandlers.HandlerGetData(type, player, data, KSSystem))
                        e.Handled = true;
                }
                catch (Exception ex)
                {
                    TShock.Log.Error(ex.ToString());
                }
            }
        }
        private void getRanks(CommandArgs args)
        {
            TSPlayer ply = args.Player;
            if (ply == null)
                return;

            if (!ply.IsLoggedIn)
                return;

            List<KSUser> li = KSSystem.Users;
            li.Sort((a, b) => b.PvPKills.CompareTo(a.PvPKills));

            if (args.Parameters.Count < 1)
            {
                var take5 = li.Take(5);
                string numberpart;
                if (li.Count <= 5)
                {
                    numberpart = li.Count.ToString();
                }
                else
                {
                    numberpart = "5";
                }
                ply.SendMessage("Kills Rank (Showing " + numberpart + " of " + li.Count + " participants)", new Color(30, 225, 212));
                int number = 1;
                foreach (KSUser user in take5)
                {
                    ply.SendMessage("[c/fbfc00:(" + number + ")]" + user.Name + " ([c/ff060b:Kills : " + user.PvPKills + "], [c/010081:Deaths : " + user.Deaths + "])", new Color(30, 225, 212));
                    number++;
                }
            }
            else
            {
                if (int.TryParse(args.Parameters[0], out int param))
                {
                    var takecustom = li.Take(param);
                    string numberpart;
                    if (li.Count <= param)
                    {
                        numberpart = li.Count.ToString();
                    }
                    else
                    {
                        numberpart = param.ToString();
                    }
                    ply.SendMessage("Kill Ranks (Showing " + numberpart + " of " + li.Count + " participants)", new Color(30, 225, 212));
                    int number = 1;
                    foreach (KSUser user in takecustom)
                    {
                        ply.SendMessage("[c/fbfc00:(" + number + ")]" + user.Name + " ([c/ff060b:Kills : " + user.PvPKills + "], [c/010081:Deaths : " + user.Deaths + "])", new Color(30, 225, 212));
                        number++;
                    }
                }
            }

        }
        private void getStats(CommandArgs args)
        {
            TSPlayer ply = args.Player;

            if (ply == null)
                return;

            if (!ply.IsLoggedIn)
                return;

            if (args.Parameters.Count < 1)
            {
                foreach (KSUser user in KSSystem.Users)
                {
                    if (user.UserID == ply.Account.ID)
                    {
                        ply.SendMessage("[KillStats] Stats for: " + user.Name, new Color(30, 225, 212));
                        ply.SendMessage("PvPKills: " + user.PvPKills, new Color(30, 225, 212));
                        ply.SendMessage("Deaths: " + user.Deaths, new Color(30, 225, 212));
                    }
                }
            }
            else
            {
                List<TSPlayer> fplayer = TSPlayer.FindByNameOrID(args.Parameters[0]);

                bool isinDatabase = false;

                if (fplayer.Count == 0)
                {
                    foreach (KSUser user in KSSystem.Users)
                    {
                        if (user.Name == args.Parameters[0])
                        {
                            isinDatabase = true;
                        }
                    }

                    if (!isinDatabase)
                    {
                        ply.SendErrorMessage("[KillStats] Player not found.");
                        return;
                    }
                }

                foreach (KSUser user in KSSystem.Users)
                {
                    if (!isinDatabase)
                    {
                        if (user.UserID == fplayer[0].Account.ID)
                        {
                            ply.SendMessage("[KillStats] Stats for: " + user.Name, new Color(30, 225, 212));
                            ply.SendMessage("PvPKills: " + user.PvPKills, new Color(30, 225, 212));
                            ply.SendMessage("Deaths: " + user.Deaths, new Color(30, 225, 212));
                            break;
                        }
                    }

                    if (isinDatabase)
                    {
                        if (user.Name == args.Parameters[0])
                        {
                            ply.SendMessage("[KillStats] Stats for: " + user.Name, new Color(79, 14, 102));
                            ply.SendMessage("PvPKills: " + user.PvPKills, new Color(104, 28, 131));
                            ply.SendMessage("Deaths: " + user.Deaths, new Color(116, 35, 145));
                            break;
                        }
                    }
                }
            }

        }

        void OnPlayerLogin(TShockAPI.Hooks.PlayerPostLoginEventArgs args)
        {
            KSUser ply = new KSUser(args.Player.Name, args.Player.Account.ID, 0, 0);
            KSSystem.add(ply, args.Player);
        }

        protected override void Dispose(bool Disposing)
        {
            if (Disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.NetGetData.Deregister(this, onGetData);
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInitialize);
                TShockAPI.Hooks.PlayerHooks.PlayerPostLogin -= OnPlayerLogin;
            }
            base.Dispose(Disposing);
        }

    }
}
