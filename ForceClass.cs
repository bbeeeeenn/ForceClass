using System.Timers;
using IL.Terraria.GameContent.ItemDropRules;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

namespace ForceClass
{
    [ApiVersion(2, 1)]
    public class TShockPlugin : TerrariaPlugin
    {
        public override string Name => "ForceClass";
        public override string Author => "TRANQUILZOIIP - github.com/bbeeeeenn";
        public override string Description => base.Description;
        public override Version Version => base.Version;
        internal static string ConfigPath = "tshock/ForceClass.json";
        private static Config Config = new();
        static readonly List<string> classChoices = new()
        {
            "warrior",
            "ranger",
            "summoner",
            "mage",
        };

        static readonly short[] MinionProjectiles =
        {
            ProjectileID.AbigailCounter,
            ProjectileID.AbigailMinion,
            ProjectileID.BabyBird,
            ProjectileID.FlinxMinion,
            ProjectileID.BabySlime,
            ProjectileID.VampireFrog,
            ProjectileID.Hornet,
            ProjectileID.FlyingImp,
            ProjectileID.VenomSpider,
            ProjectileID.JumperSpider,
            ProjectileID.DangerousSpider,
            ProjectileID.BatOfLight,
            ProjectileID.OneEyedPirate,
            ProjectileID.SoulscourgePirate,
            ProjectileID.PirateCaptain,
            ProjectileID.Smolstar,
            ProjectileID.Retanimini,
            ProjectileID.Spazmamini,
            ProjectileID.Pygmy,
            ProjectileID.Pygmy2,
            ProjectileID.Pygmy3,
            ProjectileID.Pygmy4,
            ProjectileID.StormTigerGem,
            ProjectileID.StormTigerTier1,
            ProjectileID.StormTigerTier2,
            ProjectileID.StormTigerTier3,
            ProjectileID.DeadlySphere,
            ProjectileID.Raven,
            ProjectileID.UFOMinion,
            ProjectileID.Tempest,
            ProjectileID.StardustDragon1,
            ProjectileID.StardustDragon2,
            ProjectileID.StardustDragon3,
            ProjectileID.StardustDragon4,
            ProjectileID.StardustCellMinion,
            ProjectileID.EmpressBlade,
        };

        private readonly Dictionary<string, string> PlayerClasses = new();

        public TShockPlugin(Main game)
            : base(game) { }

        public override void Initialize()
        {
            Config = Config.Load();
            // Set ClassAll to Warrior if typo error
            if (!classChoices.Contains(Config.ClassAll) && Config.SameAll)
            {
                TShock.Log.ConsoleWarn(
                    "[ForceClass] There must be a typo in your config at the ClassAll property.\n[ForceClass] Now setting it to WARRIOR temporarily."
                );
                Config.ClassAll = "warrior";
            }

            InitializeDB();

            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            PlayerHooks.PlayerPostLogin += OnPlayerLogin;
            GeneralHooks.ReloadEvent += OnReload;
        }

        private void OnGetData(GetDataEventArgs args)
        {
            if (!Config.Enabled)
                return;
            TSPlayer player = TShock.Players[args.Msg.whoAmI];
            if (player == null || !player.Active || !player.IsLoggedIn)
                return;

            switch (args.MsgID)
            {
                case PacketTypes.NpcStrike:
                    OnNpcStrike(args, player);
                    break;

                // Prevent minion summon
                case PacketTypes.ProjectileNew:
                    if (Config.SameAll && Config.ClassAll == "summoner")
                        return;
                    if (PlayerClasses[player.Name] != "summoner")
                        OnProjectileNew(args, player);
                    break;
            }
        }

        private static void OnProjectileNew(GetDataEventArgs args, TSPlayer player)
        {
            using BinaryReader reader = new(
                new MemoryStream(args.Msg.readBuffer, args.Index, args.Length)
            );

            var projectileId = reader.ReadInt16();
            _ = reader.ReadSingle();
            _ = reader.ReadSingle();
            _ = reader.ReadSingle();
            _ = reader.ReadSingle();
            var ownerId = reader.ReadByte();
            var type = reader.ReadInt16();

            if (MinionProjectiles.Contains(type))
            {
                args.Handled = true;
                player.SendData(PacketTypes.ProjectileDestroy, "", projectileId);
            }
        }

        private void OnNpcStrike(GetDataEventArgs args, TSPlayer player)
        {
            // Return if just using non weapon tools
            Item selecteditem = player.SelectedItem;
            if (
                selecteditem.pick > 0
                || selecteditem.axe > 0
                || selecteditem.hammer > 0
                || selecteditem.damage <= 0
            )
                return;

            // Whether to punish the player if wrong weapon is used
            bool Punish = false;

            string ToSwitch = Config.SameAll ? Config.ClassAll : PlayerClasses[player.Name];
            switch (ToSwitch)
            {
                case "warrior":
                    if (!player.SelectedItem.melee)
                        Punish = true;
                    break;
                case "ranger":
                    if (!player.SelectedItem.ranged)
                        Punish = true;
                    break;
                case "summoner":
                    if (!player.SelectedItem.summon)
                        Punish = true;
                    break;
                case "mage":
                    if (!player.SelectedItem.magic)
                        Punish = true;
                    break;
                case "None":
                    Punish = true;
                    break;
            }

            if (Punish)
            {
                // Freeze the player
                player.SetBuff(47, Config.PunishDuration * 60);
                if (!Config.SameAll && PlayerClasses[player.Name] == "None")
                {
                    player.SendErrorMessage(
                        "Please choose a class first!\nType '/class choose' for help."
                    );
                }
                else
                {
                    player.SendErrorMessage(
                        $"Please use a valid weapon for {(Config.SameAll ? Config.ClassAll : PlayerClasses[player.Name])}."
                    );
                }
                args.Handled = true;
            }
        }

        private static void InitializeDB()
        {
            TShock.DB.Query(
                @"
                    CREATE TABLE IF NOT EXISTS PlayerClass (
                        World INTEGER NOT NULL,
                        Username TEXT NOT NULL,
                        Class TEXT DEFAULT None
                    );
                "
            );
        }

        private void OnInitialize(EventArgs args)
        {
            Commands.ChatCommands.Add(new Command(ChooseClass, "class"));
        }

        private void OnReload(ReloadEventArgs e)
        {
            TShock.Log.ConsoleInfo("[ForceClass] Config Reloaded.");

            Config newConfig = Config.Load();
            // Set ClassAll to Warrior if typo error
            if (!classChoices.Contains(newConfig.ClassAll) && newConfig.SameAll)
            {
                TShock.Log.ConsoleWarn(
                    "[ForceClass] There must be a typo in your config at the ClassAll property.\n[ForceClass] Now setting it to WARRIOR temporarily."
                );
                newConfig.ClassAll = "warrior";
            }

            if (Config.Enabled != newConfig.Enabled)
            {
                if (newConfig.Enabled)
                {
                    TShock.Utils.Broadcast(
                        $"Class forcing is enabled!\nAll players are now required to use a weapon for their class.",
                        Color.AliceBlue
                    );
                }
                else
                {
                    TShock.Utils.Broadcast(
                        $"Class forcing is disabled!\nAll players are now free to use any weapon.",
                        Color.AliceBlue
                    );
                }
            }
            if (Config.SameAll != newConfig.SameAll)
            {
                if (newConfig.SameAll)
                {
                    TShock.Utils.Broadcast(
                        $"All players are now forced to be a {newConfig.ClassAll.ToUpper()} class!",
                        Color.AliceBlue
                    );
                }
                else
                {
                    TShock.Utils.Broadcast(
                        $"All players are now on their own class!",
                        Color.AliceBlue
                    );
                }
            }
            Config = newConfig;
        }

        private void OnPlayerLogin(PlayerPostLoginEventArgs e)
        {
            TSPlayer player = e.Player;

            using QueryResult result = TShock.DB.QueryReader(
                $@"
                    SELECT Username, Class FROM PlayerClass
                    WHERE Username=@0 AND World=@1;
                ",
                player.Account.Name,
                Main.worldID
            );
            if (!result.Read())
            {
                TShock.DB.Query(
                    $@"
                        INSERT INTO PlayerClass (World, Username)
                        VALUES (@0, @1);
                    ",
                    Main.worldID,
                    player.Account.Name
                );
                TShock.Log.ConsoleInfo(
                    $"[ForceClass] New DB row for {player.Account.Name} created."
                );
                PlayerClasses[player.Name] = "None";
                return;
            }

            string playerclass = result.Reader.Get<string>("Class");
            PlayerClasses[player.Name] = playerclass;
        }

        private void ChooseClass(CommandArgs args)
        {
            TSPlayer player = args.Player;
            if (player == null || !player.Active)
                return;

            if (!Config.Enabled)
            {
                player.SendInfoMessage("Currently disabled.");
                return;
            }
            if (Config.SameAll)
            {
                player.SendInfoMessage($"All players are forced to be a {Config.ClassAll} class.");
                return;
            }
            if (!player.IsLoggedIn)
            {
                player.SendErrorMessage("You must login first!");
                return;
            }

            // Send instructions
            if (args.Parameters.Count < 1)
            {
                player.SendMessage(
                    $"Command usage:\n/class choose <class> - Choose your class\n/class show <playername> - View player's class",
                    Color.AliceBlue
                );
                return;
            }

            if (args.Parameters[0] == "choose")
            {
                using (
                    QueryResult result = TShock.DB.QueryReader(
                        $@"
                            SELECT * FROM PlayerClass
                            WHERE Username=@0 AND World=@1;
                        ",
                        player.Account.Name,
                        Main.worldID
                    )
                )
                {
                    if (!result.Read())
                    {
                        player.SendErrorMessage("Something went wrong.");
                        return;
                    }

                    string currClass = result.Reader.Get<string>("Class");
                    if (currClass != "None")
                    {
                        player.SendErrorMessage($"You are already a '{currClass.ToUpper()}' class");
                        return;
                    }
                }

                if (args.Parameters.Count < 2)
                {
                    player.SendErrorMessage(
                        $"Usage: /class choose <class>\nAvailable classes:\n{string.Join("\n", classChoices.Select(classname => $"-{classname}"))}"
                    );
                    return;
                }

                string classinput = args.Parameters[1].ToLower();
                if (!classChoices.Contains(classinput))
                {
                    player.SendErrorMessage(
                        $"Available classes:\n{string.Join("\n", classChoices.Select(classname => $"-{classname}"))}"
                    );
                    return;
                }

                int affected = TShock.DB.Query(
                    $@"
                        UPDATE PlayerClass
                        SET Class=@0
                        WHERE Username=@1 AND World=@2;
                    ",
                    classinput,
                    player.Account.Name,
                    Main.worldID
                );
                if (affected > 1)
                {
                    player.SendErrorMessage("Something went wrong while setting your class.");
                    return;
                }

                PlayerClasses[player.Name] = classinput;

                player.SendMessage(
                    $"You are now a {classinput.ToUpper()} class!",
                    Color.Aquamarine
                );
            }
            else if (args.Parameters[0] == "show")
            {
                player.SendErrorMessage("Soon to be implemented...");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                PlayerHooks.PlayerPostLogin -= OnPlayerLogin;
                GeneralHooks.ReloadEvent -= OnReload;
            }
            base.Dispose(disposing);
        }
    }
}
