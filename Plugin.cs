//#define USERDATA


using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

namespace DangerZonePlugin
{
    public abstract class FakeEnt
    {
        public CPhysicsPropMultiplayer? RealEntity;

        bool Removed = false;
        public bool IsValid { get { return !Removed && RealEntity != null && RealEntity.IsValid; } }

        public Vector AbsOrigin
        {
            get
            {
                if (!IsValid) return Vector.Zero;
                return RealEntity.AbsOrigin;
            }
            set
            {
                if (!IsValid) return;
                Teleport(value);
            }
        }
        public QAngle AbsRotation
        {
            get
            {
                if (!IsValid) return QAngle.Zero;
                return RealEntity.AbsRotation;
            }
            set
            {
                if (!IsValid) return;
                Teleport(angles: value);
            }
        }
        
        Vector FakeVelocity = new Vector(0,0,0);

        public Vector AbsVelocity
        {
            get
            {
                if (!IsValid) return Vector.Zero;
                return FakeVelocity;
            }
            set
            {
                if (!IsValid) return;
                FakeVelocity = value;
            }
        }

        public void Teleport(Vector? position = null, QAngle? angles = null, Vector? velocity = null)
        {
            RealEntity.Teleport(position, angles, velocity);
        }

        public void Remove()
        {
            RealEntity.Remove();

            Removed = true;
        }

        public bool TickEnabled = false;
        public virtual void OnTick()
        {
            AbsOrigin += AbsVelocity/120;
            AbsVelocity /= 1.07f;
        }

        

    }
    public class Bumpmine : FakeEnt
    {
        bool stuck = false;
        Vector stuckPos = Vector.Zero;
        float lastVel = 0;

        public bool IsActive = false;

        public Bumpmine(CPhysicsPropMultiplayer prop) {
            RealEntity = prop;
            TickEnabled = true;
            lastVel = AbsOrigin.Z;
        }

        public override void OnTick()
        {
            base.OnTick();

            if (stuck)
            {
                if (AbsOrigin.X != stuckPos.X || AbsOrigin.Y != stuckPos.Y || AbsOrigin.Z != stuckPos.Z)
                {
                    AbsOrigin = new Vector(stuckPos.X, stuckPos.Y, stuckPos.Z);
                }
            }
            else
            {
                if (AbsVelocity.Z <= 0.1 && AbsOrigin.Z - lastVel > 0)
                { 
                    stuckPos = new Vector(AbsOrigin.X, AbsOrigin.Y, AbsOrigin.Z);
                    stuck = true;
                    DangerZone.Instance.AddTimer(0.5f, () =>
                    {
                        IsActive = true;
                    });
                }
                lastVel = AbsOrigin.Z;
            }
        }
    }
    public class WeaponCase : FakeEnt
    {
        bool stuck = false;

        Vector stuckPos = Vector.Zero;


        public WeaponCase(CPhysicsPropMultiplayer prop)
        {
            RealEntity = prop;
            TickEnabled = true;
        }

        public override void OnTick()
        {
            base.OnTick();

            if (stuck)
            {
                if(AbsOrigin != stuckPos)
                {
                    AbsOrigin = stuckPos;
                }
            } else
            {
                if(AbsVelocity.Z >= -20)
                {
                    stuck = true;
                    stuckPos = AbsOrigin;

                }
            }
        }
    }


    public class DZPlayer
    {
        public enum DZItems
        {
            None = 0,
            ExoJump = 1,
            Parachute = 1 << 1,
            StartParachute = 1 << 2,
            Other = 1 << 3,
        }
        public enum DZPerks
        {
            None = 0,
            ExoJump = 1,
            Parachute = 1 << 1,
            MediShot = 1 << 2,
            Zeus = 1 << 3,
            KevlarHelmet = 1 << 4,
            BonusExploreMoney = 1 << 5,

        }

        public CCSPlayerController Player;
        public DZItems Items;
        public DZPerks StartPerk;
        public DateTime freezeMsg = DateTime.MinValue;
        public DateTime exojumpNextUse = DateTime.MinValue;
        public Vector SpawnVec = Vector.Zero;

        public CDynamicProp Parachute;

        public int buyID = 0;
        public int tabletMenuID = 0;
        public DateTime dzDamageTime = DateTime.MinValue;
        public DateTime perkGetTime = DateTime.MinValue;
        public bool gotPerks = false;
        public float exploreDistance = 0;
        public Vector lastExplorePos = Vector.Zero;
        public bool isReady = false;
        public bool gotPistol = false;

        public PlayerButtons lastBtns = 0;

        public void Win()
        {
            if (Player.IsBot) return;

            UserData.User? usr = UserData.Instance().GetUser((long)(Player.SteamID - long.MaxValue), Player.PlayerName);

            if (usr == null) return;

            UserData.User user = (UserData.User)usr;
            user.Wins += 1;

            UserData.Instance().UpdateUser(user);

        }
        public void Lose()
        {

            if (Player.IsBot) return;

            UserData.User? usr = UserData.Instance().GetUser((long)(Player.SteamID - long.MaxValue), Player.PlayerName);

            if (usr == null) return;

            UserData.User user = (UserData.User)usr;
            user.Loses += 1;

            UserData.Instance().UpdateUser(user);
        }

        public void KilledPerson()
        {
        
            if (Player.IsBot) return;

            UserData.User? usr = UserData.Instance().GetUser((long)(Player.SteamID - long.MaxValue), Player.PlayerName);

            if (usr == null) return;

            UserData.User user = (UserData.User)usr;
            user.Kills += 1;

            UserData.Instance().UpdateUser(user);
        }

        public DZPlayer(CCSPlayerController player)
        {
            Player = player; 
            Items = DZItems.None;

            if (player.IsBot) return;
            UserData.Instance().GetUser((long)(player.SteamID - long.MaxValue),player.PlayerName);
            
        }

        public static DZPlayer? FindByPlayerController(CCSPlayerController player, DZPlayer[] controllers)
        {
            foreach (var controller in controllers)
            {
                if (player == controller.Player) return controller;
            }
            return null;
        }
    }
    public class DangerZone : BasePlugin
    {
        public override string ModuleName => "Danger Zone Plugin";

        public override string ModuleAuthor => "Siomek101";

        public override string ModuleVersion => "0.0.1";

        private HashSet<string> Resources = new HashSet<string>();

        private List<Bumpmine> bump_mines = new List<Bumpmine>();
        private List<WeaponCase> weapon_crates = new List<WeaponCase>();

        public static DangerZone Instance;

        public bool AddResource(string resourcePath)
        {
            if (resourcePath.Contains('/'))
            {
                resourcePath = resourcePath.Replace('/', Path.DirectorySeparatorChar);
            }

            return this.Resources.Add(resourcePath);
        }

        public bool RemoveResource(string resourcePath)
        {
            if (resourcePath.Contains('/'))
            {
                resourcePath = resourcePath.Replace('/', Path.DirectorySeparatorChar);
            }

            return this.Resources.Remove(resourcePath);
        }

        public List<DZPlayer> players = new();

        const bool DEBUG = false;
        public float dangerZoneDist = 10000;
        public bool readyToSpawnBeams = true;
        public int ReadyPeople = 0;

        CCSGameRules? gameRules = null;

        bool sv_only_knife = false;

        CCSGameRules GetGameRules() => gameRules;

        bool done = false;

        private float CalculateDistance(Vector point1, Vector point2)
        {
            float dx = point2.X - point1.X;
            float dy = point2.Y - point1.Y;
            float dz = point2.Z - point1.Z;

            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
        private float CalculateDistanceNH(Vector point1, Vector point2)
        {
            float dx = point2.X - point1.X;
            float dy = point2.Y - point1.Y;

            return (float)Math.Sqrt(dx * dx + dy * dy );
        }
        public bool IsWarmup
        {
            get
            {
                return GetGameRules() is not null && GetGameRules().WarmupPeriod;
            }
        }
        DateTime WarmupEnd = DateTime.MinValue;
        float WarmupStartTime = -1;
        public double WarmupTime
        {
            get
            {
                return ConVar.Find("mp_warmup_pausetimer")!.GetPrimitiveValue<int>() != 0 ? 0 : WarmupEnd.Subtract(DateTime.Now).TotalSeconds;
            }
        }
        bool casual_mode = false;

        public const string EXOJUMP_IMG = "data:image/webp;base64,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cC8xLjAvIiB4bWxuczp4bXBNTT0iaHR0cDovL25zLmFkb2JlLmNvbS94YXAvMS4wL21tLyIgeG1sbnM6c3RFdnQ9Imh0dHA6Ly9ucy5hZG9iZS5jb20veGFwLzEuMC9zVHlwZS9SZXNvdXJjZUV2ZW50IyIgeG1wOkNyZWF0b3JUb29sPSJBZG9iZSBQaG90b3Nob3AgMjIuMCAoV2luZG93cykiIHhtcDpDcmVhdGVEYXRlPSIyMDIyLTAyLTEyVDIxOjU5OjM5WiIgeG1wOk1vZGlmeURhdGU9IjIwMjItMDItMTJUMjE6NTk6NTRaIiB4bXA6TWV0YWRhdGFEYXRlPSIyMDIyLTAyLTEyVDIxOjU5OjU0WiIgZGM6Zm9ybWF0PSJpbWFnZS9wbmciIHBob3Rvc2hvcDpDb2xvck1vZGU9IjMiIHBob3Rvc2hvcDpJQ0NQcm9maWxlPSJzUkdCIElFQzYxOTY2LTIuMSIgeG1wTU06SW5zdGFuY2VJRD0ieG1wLmlpZDpkNjFkMjcxYy02NWFhLWYxNDItYThiNS0zMmU4MWFhNzUxYzYiIHhtcE1NOkRvY3VtZW50SUQ9ImFkb2JlOmRvY2lkOnBob3Rvc2hvcDo3Mzk2NzRkMS02MTRkLTEwNGEtODhkNy1jNDg3OTljZmQ5MDYiIHhtcE1NOk9yaWdpbmFsRG9jdW1lbnRJRD0ieG1wLmRpZDplMGZkODU2MS0wYWY3LWFmNDEtYjI0YS0yMjhhZTE5NmI1MTYiPiA8eG1wTU06SGlzdG9yeT4gPHJkZjpTZXE+IDxyZGY6bGkgc3RFdnQ6YWN0aW9uPSJjcmVhdGVkIiBzdEV2dDppbnN0YW5jZUlEPSJ4bXAuaWlkOmUwZmQ4NTYxLTBhZjctYWY0MS1iMjRhLTIyOGFlMTk2YjUxNiIgc3RFdnQ6d2hlbj0iMjAyMi0wMi0xMlQyMTo1OTozOVoiIHN0RXZ0OnNvZnR3YXJlQWdlbnQ9IkFkb2JlIFBob3Rvc2hvcCAyMi4wIChXaW5kb3dzKSIvPiA8cmRmOmxpIHN0RXZ0OmFjdGlvbj0iY29udmVydGVkIiBzdEV2dDpwYXJhbWV0ZXJzPSJmcm9tIGFwcGxpY2F0aW9uL3ZuZC5hZG9iZS5waG90b3Nob3AgdG8gaW1hZ2UvcG5nIi8+IDxyZGY6bGkgc3RFdnQ6YWN0aW9uPSJzYXZlZCIgc3RFdnQ6aW5zdGFuY2VJRD0ieG1wLmlpZDpkNjFkMjcxYy02NWFhLWYxNDItYThiNS0zMmU4MWFhNzUxYzYiIHN0RXZ0OndoZW49IjIwMjItMDItMTJUMjE6NTk6NTRaIiBzdEV2dDpzb2Z0d2FyZUFnZW50PSJBZG9iZSBQaG90b3Nob3AgMjIuMCAoV2luZG93cykiIHN0RXZ0OmNoYW5nZWQ9Ii8iLz4gPC9yZGY6U2VxPiA8L3htcE1NOkhpc3Rvcnk+IDwvcmRmOkRlc2NyaXB0aW9uPiA8L3JkZjpSREY+IDwveDp4bXBtZXRhPiA8P3hwYWNrZXQgZW5kPSJyIj8+";

        const double normal_exojump = 0.7;
        double exojump_abc = normal_exojump;

        byte[] hookSignature = { 0x4C, 0x89, 0x4C, 0x24, 0x20, 0x44, 0x89, 0x44, 0x24, 0x18, 0x89, 0x54, 0x24, 0x10, 0x55 };

        public override void Load(bool hotReload)
        {

            if (ConVar.Find("game_mode")!.GetPrimitiveValue<int>() != 0 || ConVar.Find("game_type")!.GetPrimitiveValue<int>() != 6) return;

            Instance = this;

            bool freeze = false;
            bump_mines.Clear();
            weapon_crates.Clear();



            AddCommand("only_knife", "", (a, b) =>
            {
                sv_only_knife = !sv_only_knife;
            });
            AddCommand("casual_mode", "", (a, b) =>
            {
                casual_mode = !casual_mode;
            });
            AddCommand("buggy", "", (a, b) =>
            {
                if (exojump_abc != -1) exojump_abc = -1;
                else exojump_abc = normal_exojump;
            });

            AddResource("models/props_survival/parachute/chute.vmdl"); 
            AddResource("models/weapons/w_eq_bumpmine_dropped.vmdl"); 
            AddResource("models/props_junk/plasticcrate01a.vmdl");

            gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;

            RegisterListener<Listeners.OnServerPrecacheResources>((manifest) =>
            {
                foreach (string resourcePath in this.Resources)
                {
                    manifest.AddResource(resourcePath);
                }

            });

            RegisterEventHandler<EventMapTransition>((@event, info) =>
            {

                gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;

                return HookResult.Continue;
            });
            RegisterEventHandler<EventMapShutdown>((@event, info) =>
            {
                done = false;

                return HookResult.Continue;
            });

            RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
            {
                if (@event.Userid == null) return HookResult.Continue;
                DZPlayer dzPlayer = new(@event.Userid);
                players.Add(dzPlayer);


                return HookResult.Continue;
            });
            RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
            {
                if (@event.Userid == null) return HookResult.Continue;
                DZPlayer? dzPlayer = DZPlayer.FindByPlayerController(@event.Userid, [.. players]);
                                

                if (dzPlayer != null)
                {
                    if (dzPlayer.isReady)
                    {

                        List<CCSPlayerController> players = Utilities.GetPlayers().FindAll((player) => (int)player.Team > 1 && !player.IsBot);
                        int alive = players.Count;
                        Server.PrintToChatAll($"{ReadyPeople}/{alive} players are ready.");
                        ReadyPeople--;
                    }
                    players.Remove(dzPlayer);
                }

                @event.Userid.TeamNum = 0;

                return HookResult.Continue;
            }, HookMode.Pre);
            RegisterEventHandler<EventRoundEnd>((@event, info) =>
            {
                foreach (var player in Utilities.GetPlayers())
                {
                    CCSPlayerPawn? playerPawn = player?.PlayerPawn.Value;
                    if (player == null || playerPawn == null) return HookResult.Continue;
                    DZPlayer? dzPlayer = DZPlayer.FindByPlayerController(player, players.ToArray());

                    if (dzPlayer == null)
                    {
                        dzPlayer = new(player);
                        players.Add(dzPlayer);
                    }

                    player.TeamNum = 2;
                }
                return HookResult.Continue;
            });
            RegisterEventHandler<EventPlayerSpawn>((@event, info) =>
            {
                CCSPlayerController player = @event.Userid;
                if(player == null) return HookResult.Continue;

                DZPlayer? dzPlayer = DZPlayer.FindByPlayerController(player, players.ToArray());

                if (dzPlayer == null)
                {
                    dzPlayer = new(player);
                    players.Add(dzPlayer);
                }

                //dzPlayer.freezeMsg = DateTime.Now.Add(TimeSpan.FromSeconds(10));
                dzPlayer.Items = DZPlayer.DZItems.None;
                dzPlayer.gotPistol = false;

                if (!casual_mode)
                {
                    Vector position = GetRandomPosition();
                    dzPlayer.SpawnVec = position;
                    player.PlayerPawn.Value?.Teleport(position, null, new Vector(0, 0, -50));
                    dzPlayer.lastExplorePos = position;


                }

                dzPlayer.Items |= DZPlayer.DZItems.StartParachute;
                dzPlayer.perkGetTime = casual_mode ? DateTime.MinValue : DateTime.Now.AddSeconds(10);
                dzPlayer.gotPerks = false;
                if(casual_mode)
                {
                    dzPlayer.StartPerk = DZPlayer.DZPerks.ExoJump | DZPlayer.DZPerks.Parachute | DZPlayer.DZPerks.KevlarHelmet;
                }

                player.PrintToChat($"How to use {ChatColors.Gold}tablet{ChatColors.Default}:");
                player.PrintToChat($"{ChatColors.Blue}Hold E{ChatColors.Default} - opens {ChatColors.Gold}tablet{ChatColors.Default}");
                player.PrintToChat($"{ChatColors.Blue}W{ChatColors.Default} - go up on {ChatColors.Gold}tablet{ChatColors.Default}");
                player.PrintToChat($"{ChatColors.Blue}S{ChatColors.Default} - go down on {ChatColors.Gold}tablet{ChatColors.Default}");
                player.PrintToChat($"{ChatColors.Blue}A{ChatColors.Default} - select / buy");
                player.PrintToChat($"Donate to keep server running: {ChatColors.Blue}tipply.pl/@bubbleam.pl{ChatColors.Default}");
                player.PrintToChat($"Discord with server ip: {ChatColors.Blue}discord.gg/7jG3XwytUu{ChatColors.Default}");
                player.PrintToChat($"Special thanks to: {ChatColors.Yellow}Aquarius (and his community) for playtesting and Kubixon with Vollite for promoting the server.{ChatColors.Default}");
                if (casual_mode) player.PrintToChat($"{ChatColors.Lime}CASUAL MODE{ChatColors.Default}");
                if (IsWarmup || casual_mode) switch (Random.Shared.Next(0, 5))
                {
                    case 0:
                        player.GiveNamedItem(CsItem.Glock);
                        break;
                    case 1:
                        player.GiveNamedItem(CsItem.USPS);

                        break;
                    case 2:
                        player.GiveNamedItem(CsItem.P2000);

                        break;
                    case 3:
                        player.GiveNamedItem(CsItem.P250);

                        break;
                    default:
                        player.GiveNamedItem(CsItem.Tec9);

                        break;
                }

                return HookResult.Continue;
            });
            int people = 0;
            RegisterEventHandler<EventPlayerDeath>((@event, info) =>
            {
                List<CCSPlayerController> players = Utilities.GetPlayers().FindAll((player) => player.PawnIsAlive);
                int alive = players.Count;

                //Server.PrintToChatAll(@event.Userid.PlayerName + " died from " + @event.Attacker.PlayerName + ". Place: " + (alive));

                var attacker = @event.Attacker;
                attacker.InGameMoneyServices.Account += @event.Userid.InGameMoneyServices.Account;
                Utilities.SetStateChanged(attacker, "CCSPlayerController", "m_pInGameMoneyServices");
                attacker.PrintToChat($"+${@event.Userid.InGameMoneyServices.Account} from {@event.Userid.PlayerName}");

                @event.Userid.Score = people - alive;

                DZPlayer? dzPlayer3 = DZPlayer.FindByPlayerController(@event.Userid, this.players.ToArray());

                if (dzPlayer3 == null)
                {
                    dzPlayer3 = new(@event.Userid);
                    this.players.Add(dzPlayer3);
                }

                dzPlayer3.Lose();

                Server.PrintToChatAll((alive-1) + " people left!");

                if (alive == 1)
                {
                    CCSPlayerController winner = players[0];
                    if (winner.UserId == @event.Userid.UserId) winner = players[1];
                    winner.Score = people;
                    winner.Kills.RemoveAll();
                    Server.PrintToChatAll(ChatColors.DarkRed + winner.PlayerName + ChatColors.Default + " Won! 1st place.");

                    DZPlayer? dzPlayer2 = DZPlayer.FindByPlayerController(winner, this.players.ToArray());

                    if (dzPlayer2 == null)
                    {
                        dzPlayer2 = new(winner);
                        this.players.Add(dzPlayer2);
                    }

                    dzPlayer2.Win();
                }
                if (!ConVar.Find("mp_teammates_are_enemies")!.GetPrimitiveValue<bool>())
                {
                    var winTeam = -1;
                    foreach (var player in players)
                    {

                        if (player.UserId == @event.Userid.UserId) continue;

                        if (winTeam == -1)
                        {
                            winTeam = player.TeamNum;
                            continue;
                        }
                        if (winTeam != player.TeamNum)
                        {
                            winTeam = -1;
                            break;
                        }

                    }

                    if(winTeam != -1 && !done)
                    {
                        done = true;

                        Server.PrintToChatAll("Team " + (winTeam - 1) + " won!");

                        foreach (var player in players)
                        {
                            player.CommitSuicide(false, true);
                        }
                    }
                }

                DZPlayer? dzPlayer = DZPlayer.FindByPlayerController(attacker, this.players.ToArray());

                if (dzPlayer == null)
                {
                    dzPlayer = new(attacker);
                    this.players.Add(dzPlayer);
                }

                dzPlayer.KilledPerson();

                return HookResult.Continue;
            });
            RegisterEventHandler<EventRoundStart>((@event, info) =>
            {
                bump_mines.Clear();
                weapon_crates.Clear();

                readyToSpawnBeams = true;
                freeze = false;
                people = Utilities.GetPlayers().FindAll((player) => player.PawnIsAlive).Count;

                if (IsWarmup) Server.PrintToChatAll("Warmup");

                WarmupEnd = DateTime.Now.AddSeconds(gameRules != null ? gameRules.WarmupPeriodEnd - gameRules.WarmupPeriodStart : 120);
                WarmupStartTime = gameRules.WarmupPeriodEnd - gameRules.WarmupPeriodStart;

                dangerZoneDist = 10000;

                if(!casual_mode)
                {

                    CEnvFade? fade = Utilities.CreateEntityByName<CEnvFade>("env_fade");

                    fade.Spawnflags = 1;
                    fade.Duration = 1;
                    fade.HoldDuration = 10;
                    fade.FadeColor = Color.Black;

                    fade.DispatchSpawn();

                    fade.AcceptInput("Fade");
                }

                int player_num = 0;

                foreach (var player in Utilities.GetPlayers())
                {
                    CCSPlayerPawn? playerPawn = player?.PlayerPawn.Value;
                    if (player == null || playerPawn == null) return HookResult.Continue;
                    DZPlayer? dzPlayer = DZPlayer.FindByPlayerController(player, players.ToArray());

                    if (dzPlayer == null)
                    {
                        dzPlayer = new(player);
                        players.Add(dzPlayer);
                    }

                    if(!IsWarmup && !ConVar.Find("mp_teammates_are_enemies")!.GetPrimitiveValue<bool>()) player.TeamNum = (byte)(Math.Floor(player_num / 3f)+2);

                    player_num++;

                    //dzPlayer.freezeMsg = DateTime.Now.Add(TimeSpan.FromSeconds(10));
                    dzPlayer.Items = DZPlayer.DZItems.None;

                    if(!casual_mode)
                    {
                        Vector position = GetRandomPosition();
                        player.PlayerPawn.Value?.Teleport(position, null, new Vector(0, 0, -50));

                    }
                    
                }

                // Pistols
                if(!sv_only_knife && !casual_mode) for (int i = 0; i < 100; i++)
                {
                    CPhysicsPropMultiplayer? prop = Utilities.CreateEntityByName<CPhysicsPropMultiplayer>("prop_physics_multiplayer");

                    prop.SetModel("models/props_junk/plasticcrate01a.vmdl");
                    Vector pos = GetRandomPosition();
                    pos.Z -= 300;
                    prop.Teleport(pos);
                    prop.DispatchSpawn();

                    weapon_crates.Add(new WeaponCase(prop));
                }

                return HookResult.Continue;
            });
            RegisterEventHandler<EventPlayerJump>((@event, info) => { 


                CCSPlayerController? player = @event.Userid;
                CCSPlayerPawn? playerPawn = player?.PlayerPawn.Value;
                if (player == null || playerPawn == null) return HookResult.Continue;
                DZPlayer? dzPlayer = DZPlayer.FindByPlayerController(player, players.ToArray());

                if(dzPlayer == null)
                {
                    dzPlayer = new(player);
                    players.Add(dzPlayer);
                }

                bool hasExoJump = dzPlayer.Items.HasFlag(DZPlayer.DZItems.ExoJump);
                bool hasParachute = dzPlayer.Items.HasFlag(DZPlayer.DZItems.Parachute);

                if(!hasExoJump) return HookResult.Continue;

                if (dzPlayer.exojumpNextUse.CompareTo(DateTime.Now) < 0)
                {
                    dzPlayer.exojumpNextUse = DateTime.Now.AddSeconds(exojump_abc);
                    AddTimer(0.15f, () =>
                    {
                        if (!player.Buttons.HasFlag(PlayerButtons.Jump))
                        {
                            dzPlayer.exojumpNextUse = DateTime.Now;
                            return;
                        }

                        Vector vec = new Vector(playerPawn.AbsVelocity.X, playerPawn.AbsVelocity.Y, playerPawn.AbsVelocity.Z);

                        if(player.Buttons.HasFlag(PlayerButtons.Duck)) vec *= 1.7f;
                        playerPawn.Teleport(null, null, vec);

                        var thisTimer = AddTimer(Server.TickInterval, () =>
                        {

                            if (!player.Buttons.HasFlag(PlayerButtons.Jump))
                            {
                                return;
                            }

                            vec = new Vector(playerPawn.AbsVelocity.X, playerPawn.AbsVelocity.Y, playerPawn.AbsVelocity.Z);

                            vec *= player.Buttons.HasFlag(PlayerButtons.Duck) ? 1.05f : 1;
                            vec.Z = player.Buttons.HasFlag(PlayerButtons.Duck) ? 100 : 200;

                            playerPawn.Teleport(null, null, vec);


                        }, (TimerFlags) 3);

                        AddTimer(0.5f, () => {

                            thisTimer.Kill();

                        });

                        //PlaySound("sounds/items/healthshot_success_01.vsnd", player.PlayerPawn.Value.AbsOrigin);
                       player.ExecuteClientCommand($"play sounds/items/healthshot_success_01.vsnd");


                    });
                }

                return HookResult.Continue;
            },HookMode.Post);
            Random smokeRNG = new Random();
            int smokeId = 0;
            Dictionary<string, int> smokeDict = new Dictionary<string, int>();
            RegisterListener<Listeners.OnEntitySpawned>(entity =>
            {
                if (entity.DesignerName == "smokegrenade_projectile") {
                    var projectile = new CSmokeGrenadeProjectile(entity.Handle);

                    // Changes smoke grenade colour to a random colour each time.
                    Server.NextFrame(() =>
                    {
                        if (projectile.OwnerEntity.IsValid)
                        {
                            CBaseEntity? ent = projectile.OwnerEntity.Value;
                            if (ent != null)
                            {
                                int val = 0;
                                bool seed = smokeDict.TryGetValue(ent.SubclassID.Value.ToString(), out val);
                                if (!seed)
                                {
                                    smokeId++;
                                    smokeDict.Add(ent.SubclassID.Value.ToString(), smokeId);
                                    val = smokeId;
                                }
                                smokeRNG = new Random(val);

                            }

                        }
                        projectile.SmokeColor.X = Random.Shared.NextSingle() * 255.0f;
                        projectile.SmokeColor.Y = Random.Shared.NextSingle() * 255.0f;
                        projectile.SmokeColor.Z = Random.Shared.NextSingle() * 255.0f;
                        Logger.LogInformation("Smoke grenade spawned with color {SmokeColor}", projectile.SmokeColor);
                    });

                }
                if (entity.DesignerName == "flashbang_projectile")
                {
                    CFlashbangProjectile decoy = new CFlashbangProjectile(entity.Handle);

                    Server.NextFrame(() =>
                    {
                        CPhysicsPropMultiplayer? prop = Utilities.CreateEntityByName<CPhysicsPropMultiplayer>("prop_physics_multiplayer");

                        Vector or = new Vector(decoy.AbsOrigin.X, decoy.AbsOrigin.Y, decoy.AbsOrigin.Z);
                        Vector vel = new Vector(decoy.AbsVelocity.X, decoy.AbsVelocity.Y, decoy.AbsVelocity.Z);

                        //Server.PrintToChatAll($"{or.X} {or.Y} {or.Z} - {vel.X} {vel.Y} {vel.Z}");

                        decoy.Remove();

                        prop.SetModel("models/weapons/w_eq_bumpmine_dropped.vmdl");
                        prop.Teleport(or+vel/8, null, vel);
                        prop.Spawnflags = 2;
                        prop.DispatchSpawn();

                        bump_mines.Add(new Bumpmine(prop) { AbsVelocity = vel });

                    });
                }



            });
            RegisterListener<Listeners.OnTick>(() =>
            {
                int alive = 0;
                if (readyToSpawnBeams && !IsWarmup) BeamSpawn();

                foreach (var item in bump_mines.FindAll((a) => a.TickEnabled))
                {
                    item.OnTick();
                }
                foreach (var item in weapon_crates.FindAll((a) => a.TickEnabled))
                {
                    item.OnTick();
                }

                List<CCSPlayerController> playerCtrls = Utilities.GetPlayers();
                foreach (var plCtrl in playerCtrls)
                {
                    if (plCtrl == null) continue;
                    

                    DZPlayer? dzPlayer = DZPlayer.FindByPlayerController(plCtrl, players.ToArray());

                    if (dzPlayer == null)
                    {
                        dzPlayer = new(plCtrl);
                        players.Add(dzPlayer);
                    }


                    bool hasExoJump = dzPlayer.Items.HasFlag(DZPlayer.DZItems.ExoJump); 
                    bool hasParachute = dzPlayer.Items.HasFlag(DZPlayer.DZItems.Parachute);
                    bool hasStartParachute = dzPlayer.Items.HasFlag(DZPlayer.DZItems.StartParachute);

                    if (!plCtrl.PawnIsAlive)
                    {
                        dzPlayer.Items = DZPlayer.DZItems.None;
                        dzPlayer.StartPerk = DZPlayer.DZPerks.None;
                        continue;
                    }

                    alive++;

                    CCSPlayer_ItemServices services = new CCSPlayer_ItemServices(plCtrl.PlayerPawn.Value.ItemServices!.Handle);

                    var bumpMinesRem = bump_mines.FindAll((a) => !a.IsValid || (a.IsActive && CalculateDistance(a.AbsOrigin, plCtrl.PlayerPawn.Value.AbsOrigin) < 80 ));
                    foreach (var bumpMine in bumpMinesRem)
                    {
                        if ( !bumpMine.IsValid)
                        {
                            continue;
                        }

                        // Fake it till you make it
                        Vector pos = (bumpMine.AbsOrigin - new Vector(0, 0, 100));

                        AddTimer(0.4f, () =>
                        {
                            Vector curVel = plCtrl.PlayerPawn.Value.AbsVelocity;
                            Vector diff = plCtrl.PlayerPawn.Value.AbsOrigin - pos;
                            float len = diff.Length();
                            Vector nAng = diff / len;
                            Vector vel = curVel + nAng * 1000;
                            plCtrl.PlayerPawn.Value.Teleport(null, null, vel);

                        });

                        //PlaySound("Flashbang.Explode", bumpMine.AbsOrigin);

                    }

                    foreach(var bumpMine in bumpMinesRem)
                    {
                        bump_mines.Remove(bumpMine);
                        AddTimer(5, () =>
                        {
                            if(bumpMine.IsValid) bumpMine.Remove();
                        });
                    }

                    var weaponCratesRem = weapon_crates.FindAll((a) => a == null || !a.IsValid || CalculateDistance(a.AbsOrigin, plCtrl.PlayerPawn.Value.AbsOrigin) < 60);
                    foreach (var weaponCrate in weaponCratesRem)
                    {
                        if (weaponCrate == null || !weaponCrate.IsValid) continue;
                        
                        var dist = CalculateDistance(weaponCrate.AbsOrigin, plCtrl.PlayerPawn.Value.AbsOrigin);

                        if (!dzPlayer.gotPistol)
                        {
                            switch (Random.Shared.Next(0, 5))
                            {
                                case 0:
                                    plCtrl.GiveNamedItem(CsItem.Glock);
                                    break;
                                case 1:
                                    plCtrl.GiveNamedItem(CsItem.USPS);

                                    break;
                                case 2:
                                    plCtrl.GiveNamedItem(CsItem.P2000);

                                    break;
                                case 3:
                                    plCtrl.GiveNamedItem(CsItem.P250);

                                    break;
                                default:
                                    plCtrl.GiveNamedItem(CsItem.Tec9);

                                    break;
                            }
                            dzPlayer.gotPistol = true;
                        }
                        else
                            switch (Random.Shared.Next(0, 6))
                            {

                                case 0:
                                    plCtrl.GiveNamedItem(CsItem.AK47);

                                    break;
                                case 1:
                                    plCtrl.GiveNamedItem(CsItem.M4A1S);

                                    break;
                                case 2:
                                    plCtrl.GiveNamedItem(CsItem.M4A4);

                                    break;
                                case 3:
                                    plCtrl.GiveNamedItem(CsItem.P90);

                                    break;
                                case 4:
                                    plCtrl.GiveNamedItem(CsItem.MP9);

                                    break;
                                case 5:
                                    plCtrl.GiveNamedItem(CsItem.Negev);

                                    break;


                                default:
                                    plCtrl.GiveNamedItem(CsItem.M4A4);

                                    break;
                            }

                    }

                    foreach (var weaponCrate in weaponCratesRem)
                    {
                        if (weaponCrate != null && weaponCrate.IsValid)
                        {
                            weaponCrate.Remove();
                        }
                        weapon_crates.Remove(weaponCrate);
                    }

                    int buyID = dzPlayer.buyID;

                    string[] directions = { "←", "↖", "↑", "↗", "→", "↘", "↓", "↙" };

                    //CCSPlayerPawn closest_pawn = null;
                    float distance = 5000;
                    int playersProximity = 0;

                    /*foreach (var playerctrl in playerCtrls)
                    {
                        if (playerctrl == null || playerctrl == plCtrl) continue;
                        CCSPlayerPawn pwn = playerctrl.PlayerPawn.Value;
                        float dist = CalculateDistanceNH(pwn.AbsOrigin, plCtrl.PlayerPawn.Value.AbsOrigin);
                        if (playerctrl.PawnIsAlive &&  dist < distance) playersProximity++;

                    }*/


                    if(dzPlayer.StartPerk.HasFlag(DZPlayer.DZPerks.BonusExploreMoney))
                    {
                        float exploreDistance = CalculateDistanceNH(plCtrl.PlayerPawn.Value.AbsOrigin, dzPlayer.lastExplorePos);

                        dzPlayer.exploreDistance += exploreDistance;

                        dzPlayer.lastExplorePos = new Vector(plCtrl.PlayerPawn.Value.AbsOrigin.X, plCtrl.PlayerPawn.Value.AbsOrigin.Y, plCtrl.PlayerPawn.Value.AbsOrigin.Z);

                        if (dzPlayer.exploreDistance >= 5000)
                        {

                            plCtrl.InGameMoneyServices.Account = plCtrl.InGameMoneyServices.Account + 200;
                            Utilities.SetStateChanged(plCtrl, "CCSPlayerController", "m_pInGameMoneyServices");
                            plCtrl.PrintToChat($"\x04+$200 \x01- Bonus for exploring");

                            dzPlayer.exploreDistance = 0;
                        }

                    }



                    /*string direction = "←";
                    float pitch = plCtrl.PlayerPawn.Value.EyeAngles.Y;
                    float step = 360 / directions.Length;
                    for (int i = 0; i < directions.Length; i++)
                    {
                        if(180+ pitch < step * i)
                        {
                            direction = directions[i];
                            break;
                        }
                    }*/


                    

                    if (plCtrl.Buttons.HasFlag(PlayerButtons.Use) || dzPlayer.perkGetTime.CompareTo(DateTime.Now) >= 0)
                    {
                        dzPlayer.freezeMsg = DateTime.Now.AddMinutes(10);
                        if (plCtrl.Buttons.HasFlag(PlayerButtons.Forward) && !dzPlayer.lastBtns.HasFlag(PlayerButtons.Forward))
                        {
                            DZ_Up(dzPlayer);
                        }
                        else if (plCtrl.Buttons.HasFlag(PlayerButtons.Back) && !dzPlayer.lastBtns.HasFlag(PlayerButtons.Back))
                        {

                            DZ_Down(dzPlayer);
                        }
                        else if (plCtrl.Buttons.HasFlag(PlayerButtons.Moveleft) && !dzPlayer.lastBtns.HasFlag(PlayerButtons.Moveleft))
                        {
                            DZ_Select(dzPlayer);
                        }
                    } else if(dzPlayer.lastBtns.HasFlag(PlayerButtons.Use))
                    {

                        dzPlayer.freezeMsg = DateTime.MinValue;
                    }
                    dzPlayer.lastBtns = plCtrl.Buttons;

                    float distFromDZ = dangerZoneDist-CalculateDistanceNH(new Vector(0, 0, 0), plCtrl.PlayerPawn.Value.AbsOrigin);
                    if (distFromDZ < 0 ) {

                        if (dzPlayer.dzDamageTime.CompareTo(DateTime.Now) < 0)
                        {
                            plCtrl!.PlayerPawn.Value!.Health -= 3;
                            //PlaySound("sounds/player/player_damagebody_08.vsnd", plCtrl.PlayerPawn.Value.AbsOrigin);
                            plCtrl.ExecuteClientCommand($"play sounds/player/player_damagebody_08.vsnd");
                            if (plCtrl!.PlayerPawn.Value!.Health <= 0)
                            {
                                plCtrl.CommitSuicide(true, true);
                            }

                            Utilities.SetStateChanged(plCtrl!.PlayerPawn.Value!, "CBaseEntity", "m_iHealth");
                            dzPlayer.dzDamageTime = DateTime.Now.AddSeconds(0.6);
                        }
                    }

                    string print = "";
                    if (dzPlayer.perkGetTime.CompareTo(DateTime.Now) >= 0)
                    {


                        if (!casual_mode) plCtrl.PlayerPawn.Value.Teleport(dzPlayer.SpawnVec, QAngle.Zero, Vector.Zero);

                        var print2 = $"Starting perk - {Math.Ceiling(dzPlayer.perkGetTime.Subtract(DateTime.Now).TotalSeconds)} (W - up, S - down)" +
                                $"<br>{(buyID == 0 ? "> " : "")}Exojump" +
                                $"<br>{(buyID == 1 ? "> " : "")}Parachute" +
                                $"<br>{(buyID == 2 ? "> " : "")}Armor + Kevlar" +
                                $"<br>{(buyID == 3 ? "> " : "")}Medi-Shot" +
                                $"<br>{(buyID == 4 ? "> " : "")}Taser" +
                                $"<br>{(buyID == 5 ? "> " : "")}Bonus Explore $";

                        plCtrl.PrintToCenterHtml(print2);

                        plCtrl!.PlayerPawn.Value!.Health = 100;
                        Utilities.SetStateChanged(plCtrl!.PlayerPawn.Value!, "CBaseEntity", "m_iHealth");

                        print = "";
                    }
                    else if (!dzPlayer.gotPerks)
                    {

                        dzPlayer.StartPerk = dzPlayer.buyID switch
                        {
                            0 => DZPlayer.DZPerks.ExoJump,
                            1 => DZPlayer.DZPerks.Parachute,
                            2 => DZPlayer.DZPerks.KevlarHelmet,
                            3 => DZPlayer.DZPerks.MediShot,
                            //4 => exojump_abc <= 0 ? DZPlayer.DZPerks.Zeus : DZPlayer.DZPerks.BonusExploreMoney,
                            5 => DZPlayer.DZPerks.BonusExploreMoney,
                            _ => DZPlayer.DZPerks.ExoJump
                        };
                        switch (dzPlayer.buyID)
                        {
                            case 0:
                                dzPlayer.Items |= DZPlayer.DZItems.ExoJump;
                                break;
                            case 1:
                                dzPlayer.Items |= DZPlayer.DZItems.Parachute;
                                break;
                            case 2:
                                plCtrl.PlayerPawn.Value.ArmorValue = 100;
                                services.HasHelmet = true;
                                Utilities.SetStateChanged(plCtrl.PlayerPawn.Value, "CCSPlayerPawn", "m_ArmorValue");
                                Utilities.SetStateChanged(plCtrl.PlayerPawn.Value, "CBasePlayerPawn", "m_pItemServices");
                                break;
                            case 3:
                                plCtrl.GiveNamedItem(CsItem.Healthshot);
                                break;
                            case 4:
                                //if (exojump_abc <= 0) plCtrl.GiveNamedItem(CsItem.Zeus);
                                break;
                            case 5:
                                break;
                            default:
                                dzPlayer.Items |= DZPlayer.DZItems.ExoJump;
                                break;
                        };

                        if (casual_mode)
                        {
                            plCtrl.InGameMoneyServices.Account = 10000;
                            Utilities.SetStateChanged(plCtrl, "CCSPlayerController", "m_pInGameMoneyServices");
                        }


                        dzPlayer.Items |= DZPlayer.DZItems.StartParachute;



                        dzPlayer.freezeMsg = DateTime.MinValue;

                        dzPlayer.gotPerks = true;

                    }
                    else if (dzPlayer.freezeMsg.CompareTo(DateTime.Now) < 0)
                    {
                        var exojumpTimeout = Math.Max(0,dzPlayer.exojumpNextUse.Subtract(DateTime.Now).TotalMilliseconds/100);
                        var strq = "|";

                        var eq = exojumpTimeout;
                        while (eq > 0)
                        {
                            strq += "-";
                            eq--;
                        }

                        strq += "|";

                        print = (IsWarmup ? (WarmupTime <= 0 ? "Warmup" : "Warmup: " + Math.Floor(WarmupTime)) + "<br>" : "") +
                                "<b style=\"color: red;\">Hold USE key to open tablet</b><br>" +
                                (hasExoJump ? $"<img src='https://static.wikia.nocookie.net/cswikia/images/c/c9/Exojump_hudpng.png/revision/latest/scale-to-width-down/30?cb=20220212220103' >" : "") +
                                (hasParachute ? "<img src='https://static.wikia.nocookie.net/cswikia/images/1/18/Parachute.svg/revision/latest?cb=20190509210015'>" : "") + 
                                "<br>" +
                                (hasExoJump ? (hasParachute ? $"{strq} __ " : $"{strq}") : "");
                    }
                    else
                    {
                        switch (dzPlayer.tabletMenuID)
                        {
                            case 0:
                                print = $"{(buyID == 1 ? "> " : "")}Buy Menu" +
                                        $"<br>{(buyID == 2 ? "> " : "")}Info";
                                break;
                            case 1:
                                print = $"{(buyID == 1 ? "> " : "")}Exojump: {(hasExoJump ? "Bought" : "$1300")} " +
                                        $"<br>{(buyID == 2 ? "> " : "")}Parachute: {(hasParachute ? "Bought" : "$1700")}" +
                                        $"<br>{(buyID == 3 ? "> " : "")}Armor: {(plCtrl.PlayerPawn.Value.ArmorValue == 100 ? (services.HasHelmet ? "Bought" : "Helmet $350") : $"{plCtrl.PlayerPawn.Value.ArmorValue}/100 $650")}" +
                                        $"<br>{(buyID == 4 ? "> " : "")}Health Shot: $500" +
                                        $"<br>{(buyID == 5 ? "> " : "")}Bump mine: $600" +
                                        $"<br>{(buyID == 6 ? "> " : "")}Taser: $200";

                                break;
                            case 2:
                                {
                                    var str = (Math.Floor(dzPlayer.exploreDistance) / 100).ToString();
                                    if (str.Length == 3) str += "0";
                                    print = $"{(buyID == 1 ? "> " : "")}Players in proximity: %#$!@#$<br>" +
                                            "Explore Distance: " + (dzPlayer.StartPerk.HasFlag(DZPlayer.DZPerks.BonusExploreMoney) ? $"{str}m/50m<br>" : "n/a");

                                }
                                break;
                        }

                        print = $"{(buyID == 0 ? "> " : "")}Close<br>" + print;
                    }
                        
                    if(print != "") plCtrl.PrintToCenterHtml($"Zone: {Math.Max(0,Math.Floor(distFromDZ/100))}m {(distFromDZ < 200 ? (distFromDZ < 0 ? "You are in danger zone!" : "WARNING") : "")}<br>" + print);
                    if (plCtrl.PlayerPawn.IsValid && plCtrl.PlayerPawn.Value != null && (hasExoJump && plCtrl.PlayerPawn.Value.AbsVelocity.Z < -450))
                    {
                        Vector vel = new Vector(plCtrl.PlayerPawn.Value.AbsVelocity.X, plCtrl.PlayerPawn.Value.AbsVelocity.Y, plCtrl.PlayerPawn.Value.AbsVelocity.Z);

                        if (vel.Z < -450)
                        {
                            vel.Z += 7;

                            plCtrl.PlayerPawn.Value.Teleport(null, null, vel);
                        }
                    }

                    if (plCtrl.PlayerPawn.IsValid && plCtrl.PlayerPawn.Value != null && (hasStartParachute || (hasParachute && plCtrl.PlayerPawn.Value.AbsVelocity.Z < -450)))
                    {

                        Vector vel = new Vector(plCtrl.PlayerPawn.Value.AbsVelocity.X, plCtrl.PlayerPawn.Value.AbsVelocity.Y, plCtrl.PlayerPawn.Value.AbsVelocity.Z);

                        if (dzPlayer.Parachute == null || !dzPlayer.Parachute.IsValid)
                        {
                            dzPlayer.Parachute = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");

                            //prop.SetModel("models/weapons/w_eq_bumpmine_dropped.vmdl_c");
                            dzPlayer.Parachute.SetModel("models/props_survival/parachute/chute.vmdl");

                            dzPlayer.Parachute.Teleport(plCtrl.PlayerPawn.Value.AbsOrigin);
                            dzPlayer.Parachute.DispatchSpawn();
                        }
                        else
                        {
                            dzPlayer.Parachute.Teleport(plCtrl.PlayerPawn.Value.AbsOrigin);
                        }

                        if (vel.Z < -450)
                        {
                            if (vel.Z < -500)
                                vel.Z += 5;
                            if (vel.Z < -550)
                                vel.Z += 50;
                            vel.Z += 5;

                            plCtrl.PlayerPawn.Value.Teleport(null, null, vel);
                        } 

                        if(hasStartParachute)
                        {

                            if (vel.Z > -20)
                            {
                                dzPlayer.Items &= ~DZPlayer.DZItems.StartParachute;

                            }
                            if (vel.Z < -250)
                                vel.Z += 5;
                            if (vel.Z < -300)
                                vel.Z += 10;
                            vel.Z += 5;

                            plCtrl.PlayerPawn.Value.Teleport(null, null, vel);
                        }

                    } else
                    {
                        if (dzPlayer.Parachute != null && dzPlayer.Parachute.IsValid)
                            dzPlayer.Parachute.Teleport(Vector.Zero);
                    }

                }

                if (!casual_mode)
                {
                    if (!IsWarmup) dangerZoneDist -= alive < 5 ? (alive < 4 ? (alive < 3 ? 3 : 2) : 1f) : 0.3f;
                    if (dangerZoneDist < 400) dangerZoneDist = 400;
                }
            });

            AddCommand("dz_give_exojump", "Gives exojump", (controller, info) => {
                if (!DEBUG) return;
                if (controller == null) return;

                DZPlayer? dzPlayer = DZPlayer.FindByPlayerController(controller, players.ToArray());

                if (dzPlayer == null)
                {
                    dzPlayer = new(controller);
                    players.Add(dzPlayer);
                }

                dzPlayer.Items |= DZPlayer.DZItems.ExoJump;

            });
            AddCommand("dz_give_parachute", "Gives exojump", (controller, info) => {
                if (!DEBUG) return;

                if (controller == null) return;

                DZPlayer? dzPlayer = DZPlayer.FindByPlayerController(controller, players.ToArray());

                if (dzPlayer == null)
                {
                    dzPlayer = new(controller);
                    players.Add(dzPlayer);
                }

                dzPlayer.Items |= DZPlayer.DZItems.Parachute;

            });
            AddCommand("dz_remove_items", "Removes items", (controller, info) =>
            {

                if (!DEBUG) return;
                if (controller == null) return;

                DZPlayer? dzPlayer = DZPlayer.FindByPlayerController(controller, players.ToArray());

                if (dzPlayer == null)
                {
                    dzPlayer = new(controller);
                    players.Add(dzPlayer);
                }

                dzPlayer.Items = DZPlayer.DZItems.None;

            });

            

            AddCommand("dz_tablet", "Open/Close tablet menu", (controller, info) => {


                if (controller == null) return;

                DZPlayer? dzPlayer = DZPlayer.FindByPlayerController(controller, players.ToArray());

                if (dzPlayer == null)
                {
                    dzPlayer = new(controller);
                    players.Add(dzPlayer);
                }

                dzPlayer.freezeMsg = dzPlayer.freezeMsg.CompareTo(DateTime.Now) < 0 ? DateTime.Now.AddMinutes(10) : DateTime.MinValue;
                dzPlayer.buyID = 0;
                dzPlayer.tabletMenuID = 0;
            });

            AddCommand("dz_down", "Buy - go down", (controller, info) => {


                if (controller == null) return;

                DZPlayer? dzPlayer = DZPlayer.FindByPlayerController(controller, players.ToArray());

                if (dzPlayer == null)
                {
                    dzPlayer = new(controller);
                    players.Add(dzPlayer);
                }

                DZ_Down(dzPlayer);
            });
            AddCommand("dz_up", "Buy - go up", (controller, info) => {


                if (controller == null) return;

                DZPlayer? dzPlayer = DZPlayer.FindByPlayerController(controller, players.ToArray());

                if (dzPlayer == null)
                {
                    dzPlayer = new(controller);
                    players.Add(dzPlayer);
                }

                DZ_Up(dzPlayer);
            });
            AddCommand("dz_buy", "Buy current selection", (controller, info) => {


                if (controller == null) return;

                DZPlayer? dzPlayer = DZPlayer.FindByPlayerController(controller, players.ToArray());

                if (dzPlayer == null)
                {
                    dzPlayer = new(controller);
                    players.Add(dzPlayer);
                }

                if( dzPlayer.freezeMsg.CompareTo(DateTime.Now) < 0 )
                {
                    dzPlayer.freezeMsg = DateTime.Now.AddMinutes(10);
                    dzPlayer.buyID = 0;
                    dzPlayer.tabletMenuID = 0;
                    return;
                }

                DZ_Select(dzPlayer);
            });
            moneyTimer = AddTimer(30, () => {
                
                foreach (var controller in Utilities.GetPlayers())
                {

                    if (controller == null) continue;

                    DZPlayer? dzPlayer = DZPlayer.FindByPlayerController(controller, players.ToArray());

                    if (dzPlayer == null)
                    {
                        dzPlayer = new(controller);

                        players.Add(dzPlayer);
                    }

                    if (!controller.PawnIsAlive) continue;

                    controller.InGameMoneyServices.Account = controller.InGameMoneyServices.Account + 200;
                    Utilities.SetStateChanged(controller, "CCSPlayerController", "m_pInGameMoneyServices");
                    controller.PrintToChat($"\x04+$200 \x01- Bonus for living");
                }

            }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);
            AddCommand("buy", "Buys", (controller, info) => {
                PlayerBuy(controller, info.ArgString);
            });

            AddCommand("dz_buy_exojump", "Buys ExoJump", (controller, info) => {

                if (!DEBUG) return;
                if (controller == null) return;

                DZPlayer? dzPlayer = DZPlayer.FindByPlayerController(controller, players.ToArray());

                if (dzPlayer == null)
                {
                    dzPlayer = new(controller);
                    players.Add(dzPlayer);
                }

                if (dzPlayer.Items.HasFlag(DZPlayer.DZItems.ExoJump)) return;

                if (controller.InGameMoneyServices.Account < 2000)
                {
                    controller.PrintToChat("Not enough money to buy ExoJump!");
                    return;
                }

               controller.InGameMoneyServices.Account = controller.InGameMoneyServices.Account - 2000;

                dzPlayer.Items |= DZPlayer.DZItems.ExoJump;
                controller.PrintToChat("Bought ExoJump");

            });
            AddCommand("dz_buy_parachute", "Buys parachute", (controller, info) => {

                if (!DEBUG) return;
                if (controller == null) return;

                DZPlayer? dzPlayer = DZPlayer.FindByPlayerController(controller, players.ToArray());

                if (dzPlayer == null)
                {
                    dzPlayer = new(controller);
                    players.Add(dzPlayer);
                }

                if (dzPlayer.Items.HasFlag(DZPlayer.DZItems.Parachute)) return;

                if (controller.InGameMoneyServices.Account < 2000)
                {
                    controller.PrintToChat("Not enough money to buy a parachute!");
                    return;
                }
                controller.InGameMoneyServices.Account = controller.InGameMoneyServices.Account - 2000;

                dzPlayer.Items |= DZPlayer.DZItems.Parachute;
                controller.PrintToChat("Bought a parachute");

            });

            AddCommand("ready", "readies up", (controller, info) => {
                if (controller == null) return;

                DZPlayer? dzPlayer = DZPlayer.FindByPlayerController(controller, players.ToArray());

                if (dzPlayer == null)
                {
                    dzPlayer = new(controller);
                    players.Add(dzPlayer);
                }

                if (!IsWarmup)
                {
                    controller.PrintToChat("You cannot ready up in warmup.");
                    return;
                }

                if (dzPlayer.isReady)
                {
                    dzPlayer.isReady = false;
                    ReadyPeople--;
                    controller.PrintToChat("You are now not ready. Type !ready to ready up.");
                }
                else
                {
                    dzPlayer.isReady = true;
                    ReadyPeople++;
                    controller.PrintToChat("You are now ready. Type !ready to not be ready.");
                }


                List<CCSPlayerController> playes = Utilities.GetPlayers().FindAll((player) => (int)player.Team > 1 && !player.IsBot);
                int alive = playes.Count;
                Server.PrintToChatAll($"{ReadyPeople}/{(alive < 2 ? 2 : alive)} players are ready.");

                if (alive >= 2 && alive >= ReadyPeople)
                {
                    Server.PrintToChatAll($"Everyone is ready. Starting in 3 seconds.");

                    AddTimer(3, () =>
                    {
                        if (alive >= 2 && alive == ReadyPeople)
                        {
                            Server.PrintToChatAll($"Starting...");
                            Server.ExecuteCommand("mp_warmup_end");

                            foreach (var item in players)
                            {
                                item.isReady = false;
                            }
                        }
                        else
                        {
                            Server.PrintToChatAll($"Cannot start.");

                        }

                    });

                }
            });

            AddCommand("gimme_bump", "", (controller, info) => {

                if (controller == null) return;

                controller.GiveNamedItem(CsItem.Flashbang);

            });
            AddCommand("fast_zon", "", (controller, info) => {

                if (controller == null) return;

                dangerZoneDist -= 20;

            });
        }


        CounterStrikeSharp.API.Modules.Timers.Timer? beamTimer;
        CounterStrikeSharp.API.Modules.Timers.Timer? moneyTimer;

        public void BeamSpawn()
        {
            if (beamTimer != null)
            {
                beamTimer.Kill();
            }

            readyToSpawnBeams = false;

            int beamStep = 30;
            int beamCount = 50;


            Vector[] sides = [new Vector(-0.5f, -0.5f, 0), new Vector(0f, -0.75f, 0), new Vector(0.5f, -0.5f, 0), new Vector(0.75f, 0, 0), new Vector(0.5f, 0.5f, 0), new Vector(0f, 0.75f, 0), new Vector(-0.5f, 0.5f, 0), new Vector(-0.75f, 0, 0)];
            CEnvBeam[][] beam = new CEnvBeam[beamCount][];

            for (int j = 0; j < beamCount; j++)
            {
                Vector pos = new Vector(0, 0, (j + 30) * beamStep);
                beam[j] = new CEnvBeam[sides.Length];
                for (int i = 0; i < sides.Length; i++)
                {
                    CEnvBeam? prop = Utilities.CreateEntityByName<CEnvBeam>("env_beam");
                    prop.Render = Color.Red;
                    prop.Width = 15;
                    float size = dangerZoneDist * 1.34f;
                    MoveLaser(prop, pos + sides[i == 0 ? sides.Length - 1 : i - 1] * size, pos + sides[i] * size);
                    prop.DispatchSpawn();
                    beam[j][i] = prop;
                }

            }
            beamTimer = AddTimer(0.02f, () => {

                for (int j = 0; j < beam.Length; j++)
                {
                    Vector pos = new Vector(0, 0, (j + 30) * beamStep);



                    for (int i = 0; i < sides.Length; i++)
                    {
                        CEnvBeam? prop = beam[j][i];//Utilities.CreateEntityByName<CEnvBeam>("env_beam");
                        if (prop != null && prop.IsValid) {
                            float size = dangerZoneDist * 1.34f;
                            MoveLaser(prop, pos + sides[i == 0 ? sides.Length - 1 : i - 1] * size, pos + sides[i] * size);
                        } 
                    }

                }

            },CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT | CounterStrikeSharp.API.Modules.Timers.TimerFlags.STOP_ON_MAPCHANGE);
        }
        public override void Unload(bool hotReload)
        {
            beamTimer?.Kill();
            moneyTimer?.Kill();
            UserData.Instance().Close();
            base.Unload(hotReload);
        }
        public void MoveLaser(CEnvBeam? laser, Vector start, Vector end)
        {
            if (laser == null)
            {
                return;
            }

            // set pos
            laser.Teleport(start);

            // end pos
            // NOTE: we cant just move the whole vec
            laser.EndPos.X = end.X;
            laser.EndPos.Y = end.Y;
            laser.EndPos.Z = end.Z;

            Utilities.SetStateChanged(laser, "CBeam", "m_vecEndPos");
        }

        // Thanks to @joakimo on discord
        [ConsoleCommand("tprandom")]
        public void TeleportRandomCommand(CCSPlayerController player, CommandInfo command)
        {
            if (!DEBUG) return;
            Vector position = GetRandomPosition();
            player.PlayerPawn.Value?.Teleport(position);
        }

        public void PlayerBuy(CCSPlayerController controller, string ArgString)
        {

            if (controller == null) return;

            DZPlayer? dzPlayer = DZPlayer.FindByPlayerController(controller, players.ToArray());

            if (dzPlayer == null)
            {
                dzPlayer = new(controller);
                players.Add(dzPlayer);
            }
            //if (freezeMsg.CompareTo(DateTime.Now) < 0 && !DEBUG)
            //{
            //    controller.PrintToChat("Cannot buy right now.");

            //    return;
            //}

            DZPlayer.DZItems item = DZPlayer.DZItems.None;
            string[] args = ArgString.ToLower().Split(" ");

            foreach (string arg in args)
            {

                string itemName = "";
                int moneyNeeded = 1300;

                CCSPlayer_ItemServices services = new CCSPlayer_ItemServices(controller.PlayerPawn.Value.ItemServices!.Handle);

                if (arg == "exojump" || arg == "1")
                {
                    item = DZPlayer.DZItems.ExoJump;
                    itemName = item.ToString();
                    moneyNeeded = 1300;
                }
                else if (arg == "parachute" || arg == "2")
                {
                    item = DZPlayer.DZItems.Parachute;
                    itemName = item.ToString();
                    moneyNeeded = 1700;
                }
                else if (arg == "armor" || arg == "3")
                {
                    item = DZPlayer.DZItems.Other;
                    itemName = "Armor";
                    moneyNeeded = 650;

                    if (controller.PawnArmor == 100)
                    {
                        item = DZPlayer.DZItems.Other;
                        itemName = "Helmet";
                        moneyNeeded = 350;
                        if (services.HasHelmet)
                        {
                            continue;
                        }
                    }
                }
                else if (arg == "healthshot" || arg == "4")
                {
                    item = DZPlayer.DZItems.Other;
                    itemName = "Healthshot";
                    moneyNeeded = 500;
                }
                else if (arg == "bumpmine" || arg == "5")
                {
                    item = DZPlayer.DZItems.Other;
                    itemName = "Bumpmine";
                    moneyNeeded = 500;
                }
                else if(arg == "taser" || arg == "6")
                {
                    item = DZPlayer.DZItems.Other;
                    itemName = "Taser";
                    moneyNeeded = 200;

                } /*else if(arg == "smoke" || arg == "7")
                {

                    item = DZPlayer.DZItems.Other;
                    itemName = "Smoke";
                    moneyNeeded = 300;
                }*/
                /*else if (arg == "weapons" || arg == "6")
                {
                    item = DZPlayer.DZItems.Other;
                    itemName = "Weapons";
                    moneyNeeded = 2700;
                }*/

                if (item != DZPlayer.DZItems.Other && dzPlayer.Items.HasFlag(item)) continue;

                if (controller.InGameMoneyServices.Account < moneyNeeded && !DEBUG)
                {
                    controller.PrintToChat("Not enough money to buy " + itemName);
                    continue;
                }

                if (!DEBUG)
                {
                    controller.InGameMoneyServices.Account = controller.InGameMoneyServices.Account - moneyNeeded;
                    Utilities.SetStateChanged(controller, "CCSPlayerController", "m_pInGameMoneyServices");
                }

                if (item != DZPlayer.DZItems.Other) dzPlayer.Items |= item;
                else
                {
                    switch (itemName.ToLower())
                    {
                        case "armor":

                            controller.PlayerPawn.Value.ArmorValue = 100;
                            Utilities.SetStateChanged(controller.PlayerPawn.Value, "CCSPlayerPawn", "m_ArmorValue");
                            break;
                        case "helmet":
                            services.HasHelmet = true;
                            Utilities.SetStateChanged(controller.PlayerPawn.Value, "CBasePlayerPawn", "m_pItemServices");
                            break;
                        case "healthshot":
                            controller.GiveNamedItem(CsItem.Healthshot);
                            break;
                        case "smoke":
                            controller.GiveNamedItem(CsItem.Smoke);
                            break;
                        case "taser":
                            //if(exojump_abc <= 0) controller.GiveNamedItem(CsItem.Taser);
                            break;
                        case "bumpmine":
                            controller.GiveNamedItem(CsItem.Flashbang);
                            break;
                        case "weapons":
                            CsItem randomItem;
                            int random = Random.Shared.Next(0, 9);
                            switch (random)
                            {
                                case 0: // ak47
                                    randomItem = CsItem.AK47;
                                    break;
                                case 1: // m4a4
                                    randomItem = CsItem.M4A4;
                                    break;
                                case 2: // m4a1
                                    randomItem = CsItem.M4A1S;
                                    break;
                                case 3: // p90
                                    randomItem = CsItem.P90;
                                    break;
                                case 4: // mp9
                                    randomItem = CsItem.MP9;
                                    break;
                                case 5: // mp7
                                    randomItem = CsItem.MP7;
                                    break;
                                case 6: // negev
                                    randomItem = CsItem.Negev;
                                    break;
                                case 7: // ssg
                                    randomItem = CsItem.SSG08;
                                    break;
                                case 8: // awp
                                    randomItem = CsItem.AWP;
                                    break;
                                default:
                                    randomItem = CsItem.AWP;
                                    break;
                            }
                            controller.GiveNamedItem(randomItem);
                            break;
                        default:
                            controller.PrintToChat("unknown item");
                            break;


                    }
                }
                controller.PrintToChat("Bought " + itemName);
            }
        }

        void PlaySound(string sound, Vector pos)
        {
            CSoundEventEntityAlias_snd_event_point snd = Utilities.CreateEntityByName<CSoundEventEntityAlias_snd_event_point>("snd_event_point");
            snd.SoundName = sound;
            snd.Teleport(pos);
            snd.StartOnSpawn = true;
            snd.DispatchSpawn();

            AddTimer(10, () => {

                snd.Remove();
            
            });
        }

        Vector GetRandomPosition()
        {
            // assumes the map is mirage
            int minX = -1834;
            int minY = -2525;
            int maxX = 1347;
            int maxY = 720;
            int Z = 2400;
            if(Server.MapName == "de_vertigo")
            {
                Z = 11921;
            }
            // assumes blacksite
            if (Server.MapName == "dz_blacksite")
            {
                minX = -6554;
                minY = -6586;
                maxX = 4901;
                maxY = 6386;
            }
            if (Server.MapName == "dz_sirocco") { 
                // assumes sirocc
                minX = -5676;
                minY = -6363;
                maxX = 4857;
                maxY = 6292;
            }


            return new Vector(Random.Shared.Next(minX, maxX), Random.Shared.Next(minY, maxY),Z);
        }

        public void DZ_Select(DZPlayer dzPlayer)
        {
            if(dzPlayer.perkGetTime.CompareTo(DateTime.Now) >= 0)
            {
                return;
            } 
            if (dzPlayer.tabletMenuID != 0 && dzPlayer.buyID == 0)
            {
                dzPlayer.buyID = 0;
                dzPlayer.tabletMenuID = 0;
                return;
            }
            switch (dzPlayer.tabletMenuID)
            {
                case 0:
                    if (dzPlayer.buyID == 0)
                    {
                        dzPlayer.freezeMsg = DateTime.MinValue;
                        dzPlayer.tabletMenuID = 0;
                        return;
                    }
                    dzPlayer.tabletMenuID = dzPlayer.buyID;
                    dzPlayer.buyID = 0;

                    break;
                case 1:
                    PlayerBuy(dzPlayer.Player, dzPlayer.buyID.ToString());
                    break;

                default:
                    break;
            }
        }

        public void DZ_Down(DZPlayer dzPlayer)
        {
            dzPlayer.buyID++;
            int max = dzPlayer.tabletMenuID switch { 0 => 2, 1 => 6, 2 => 1, _ => 0 };
            if (dzPlayer.perkGetTime.CompareTo(DateTime.Now) >= 0) max = 5;
            if (dzPlayer.buyID > max) dzPlayer.buyID = max;
        }

        private void DZ_Up(DZPlayer dzPlayer)
        {

            dzPlayer.buyID--;
            if (dzPlayer.buyID < 0) dzPlayer.buyID = 0;
        }

    }
}
