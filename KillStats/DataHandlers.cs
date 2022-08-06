using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Streams;
using Terraria;
using Terraria.DataStructures;
using TShockAPI;

namespace KillStats
{
    public delegate bool GetDataHandlerDelegate(GetDataHandlerArgs args);

    public class GetDataHandlerArgs : EventArgs
    {
        public TSPlayer Player { get; private set; }
        public MemoryStream Data { get; private set; }

        public Player TPlayer
        {
            get { return Player.TPlayer; }
        }

        public GetDataHandlerArgs(TSPlayer player, MemoryStream data)
        {
            Player = player;
            Data = data;
        }
    }

    public static class GetDataHandlers
    {
        private static Dictionary<PacketTypes, GetDataHandlerDelegate> GetDataHandlerDelegates;
        public static DB KSSystem;

        public static void InitGetDataHandler()
        {
            GetDataHandlerDelegates = new Dictionary<PacketTypes, GetDataHandlerDelegate>
            {
                { PacketTypes.PlayerDeathV2, HandlePlayerKillMeV2 },
                { PacketTypes.PlayerHurtV2, HandlePlayerDamageV2 }
            };

        }

        public static bool HandlerGetData(PacketTypes type, TSPlayer player, MemoryStream data, DB dbsystem)
        {
            GetDataHandlerDelegate handler;
            KSSystem = dbsystem;

            if (GetDataHandlerDelegates.TryGetValue(type, out handler))
            {
                try
                {
                    return handler(new GetDataHandlerArgs(player, data));
                }
                catch (Exception ex)
                {
                    TShock.Log.Error(ex.ToString());
                }
            }
            return false;
        }

        public class PlayerDamageEventArgs : GetDataHandledEventArgs
        {
            /// <summary>
            /// The Terraria playerID of the player
            /// </summary>
            public byte ID { get; set; }
            /// <summary>
            /// The direction the damage is occurring from
            /// </summary>
            public byte Direction { get; set; }
            /// <summary>
            /// Amount of damage
            /// </summary>
            public short Damage { get; set; }
            /// <summary>
            /// If the player has PVP on
            /// </summary>
            public bool PVP { get; set; }
            /// <summary>
            /// Is the damage critical?
            /// </summary>
            public bool Critical { get; set; }
            /// <summary>The reason the player took damage and/or died.</summary>
            public PlayerDeathReason PlayerDeathReason { get; set; }
        }
        /// <summary>
        /// PlayerDamage - Called when a player is damaged
        /// </summary>
        public static HandlerList<PlayerDamageEventArgs> PlayerDamage = new HandlerList<PlayerDamageEventArgs>();
        private static bool OnPlayerDamage(TSPlayer player, MemoryStream data, byte id, byte dir, short dmg, bool pvp, bool crit, PlayerDeathReason playerDeathReason)
        {
            if (PlayerDamage == null)
                return false;

            var args = new PlayerDamageEventArgs
            {
                Player = player,
                Data = data,
                ID = id,
                Direction = dir,
                Damage = dmg,
                PVP = pvp,
                Critical = crit,
                PlayerDeathReason = playerDeathReason,
            };
            PlayerDamage.Invoke(null, args);
            return args.Handled;
        }

        /// <summary>
        /// For use in a KillMe event
        /// </summary>
        public class KillMeEventArgs : GetDataHandledEventArgs
        {
            /// <summary>
            /// The Terraria playerID of the player
            /// </summary>
            public byte PlayerId { get; set; }
            /// <summary>
            /// The direction the damage is coming from (?)
            /// </summary>
            public byte Direction { get; set; }
            /// <summary>
            /// Amount of damage dealt
            /// </summary>
            public short Damage { get; set; }
            /// <summary>
            /// Player's current pvp setting
            /// </summary>
            public bool Pvp { get; set; }
            /// <summary>The reason the player died.</summary>
            public PlayerDeathReason PlayerDeathReason { get; set; }
        }
        /// <summary>
        /// KillMe - Terraria's crappy way of handling damage from players
        /// </summary>
        public static HandlerList<KillMeEventArgs> KillMe = new HandlerList<KillMeEventArgs>();
        private static bool OnKillMe(TSPlayer player, MemoryStream data, byte plr, byte direction, short damage, bool pvp, PlayerDeathReason playerDeathReason)
        {
            if (KillMe == null)
                return false;

            var args = new KillMeEventArgs
            {
                Player = player,
                Data = data,
                PlayerId = plr,
                Direction = direction,
                Damage = damage,
                Pvp = pvp,
                PlayerDeathReason = playerDeathReason,
            };
            KillMe.Invoke(null, args);
            return args.Handled;
        }


        private static bool HandlePlayerDamageV2(GetDataHandlerArgs args)
        {
            var id = args.Data.ReadInt8();
            PlayerDeathReason playerDeathReason = PlayerDeathReason.FromReader(new BinaryReader(args.Data));
            var dmg = args.Data.ReadInt16();
            var direction = (byte)(args.Data.ReadInt8() - 1);
            var bits = (BitsByte)(args.Data.ReadByte());
            var crit = bits[0];
            var pvp = bits[1];
            TSPlayer sender = TShock.Players[playerDeathReason._sourcePlayerIndex];

            if (OnPlayerDamage(args.Player, args.Data, id, direction, dmg, pvp, crit, playerDeathReason))
                return true;

            TSPlayer victim = TShock.Players[id];

            if (victim.IsLoggedIn)
            {
                foreach (KSUser user in KSSystem.Users)
                {
                    if (user.UserID == victim.Account.ID)
                    {
                        user.Killer = sender;
                    }

                }
            }
            return false;
        }

        private static bool HandlePlayerKillMeV2(GetDataHandlerArgs args)
        {
            var id = args.Data.ReadInt8();
            PlayerDeathReason playerDeathReason = PlayerDeathReason.FromReader(new BinaryReader(args.Data));
            var dmg = args.Data.ReadInt16();
            var direction = (byte)(args.Data.ReadInt8() - 1);
            BitsByte bits = (BitsByte)args.Data.ReadByte();
            bool pvp = bits[0];

            if (OnKillMe(args.Player, args.Data, id, direction, dmg, pvp, playerDeathReason))
                return true;

            TSPlayer victim = TShock.Players[id];

            if (victim.IsLoggedIn)
            {
                if (pvp)
                {
                    KSUser userfound = null;
                    foreach (KSUser user in KSSystem.Users)
                    {
                        if (user.UserID == victim.Account.ID)
                        {
                            userfound = user;
                            user.Deaths += 1;
                            KSSystem.update(user);
                            TSPlayer.All.SendInfoMessage(user.Killer.Name + " [i/1:" + playerDeathReason._sourceItemType + "] " + victim.Name);
                        }
                    }

                    foreach (KSUser usr in KSSystem.Users)
                    {
                        if (usr.UserID == userfound.Killer.Account.ID)
                        {
                            usr.PvPKills += 1;
                            KSSystem.update(usr);
                        }
                    }
                }
            }
            return false;
        }
    }
}