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
        private readonly Dictionary<string, DateTime> LastMessageSent = new();

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

            TShock.DB.Query(
                @"
                    CREATE TABLE IF NOT EXISTS PlayerClass (
                        World INTEGER NOT NULL,
                        Username TEXT NOT NULL,
                        Class TEXT DEFAULT None
                    );
                "
            );

            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            PlayerHooks.PlayerPostLogin += OnPlayerLogin;
            GeneralHooks.ReloadEvent += OnReload;
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
        }

        private void OnInitialize(EventArgs args)
        {
            Commands.ChatCommands.Add(new Command(ChooseClass, "class") { AllowServer = false });
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
                        Color.LightCyan
                    );
                }
                else
                {
                    TShock.Utils.Broadcast(
                        $"Class forcing is disabled!\nAll players are now free to use any weapon.",
                        Color.LightCyan
                    );
                }
            }
            if (Config.SameAll != newConfig.SameAll)
            {
                if (newConfig.SameAll)
                {
                    TShock.Utils.Broadcast(
                        $"All players are now forced to be a {newConfig.ClassAll.ToUpper()} class!",
                        Color.LightCyan
                    );
                }
                else
                {
                    TShock.Utils.Broadcast(
                        $"All players are now on their own class!",
                        Color.LightCyan
                    );
                }
            }
            Config = newConfig;
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
                // Prevent minion summon
                case PacketTypes.ProjectileNew:
                    if (
                        (Config.SameAll && Config.ClassAll != "summoner")
                        || (!Config.SameAll && PlayerClasses[player.Name] != "summoner")
                    )
                        PreventMinion(args, player);
                    break;

                case PacketTypes.NpcStrike:
                    OnNpcStrike(args, player);
                    break;
            }
        }

        private void PreventMinion(GetDataEventArgs args, TSPlayer player)
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
                player.SendData(PacketTypes.ProjectileDestroy, "", projectileId, ownerId);
                if (PlayerClasses[player.Name] == "None")
                {
                    SendMessage(
                        player,
                        $"You have to be a SUMMONER to do that.\nType '/class choose' for help.",
                        Color.Red
                    );
                }
                else if (PlayerClasses[player.Name] != "summoner")
                {
                    SendMessage(player, $"You have to be a SUMMONER to do that.", Color.Red);
                }
                args.Handled = true;
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
                if (!Config.SameAll && PlayerClasses[player.Name] == "None")
                {
                    SendMessage(
                        player,
                        "Please choose a class first!\nType '/class choose' for help.",
                        Color.Red
                    );
                }
                else
                {
                    SendMessage(
                        player,
                        $"Please use a valid weapon for {(Config.SameAll ? Config.ClassAll.ToUpper() : PlayerClasses[player.Name].ToUpper())}.",
                        Color.Red
                    );
                }
                args.Handled = true;
            }
        }

        private void ChooseClass(CommandArgs args)
        {
            TSPlayer player = args.Player;
            if (player == null || !player.Active)
                return;

            if (!Config.Enabled)
            {
                player.SendMessage("Currently disabled.", Color.LightCyan);
                return;
            }
            if (Config.SameAll)
            {
                player.SendMessage(
                    $"All players are forced to be a {Config.ClassAll.ToUpper()} class.",
                    Color.LightCyan
                );
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
                    Color.LightCyan
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
                        player.SendErrorMessage($"You are already a {currClass.ToUpper()} class");
                        return;
                    }
                }

                if (args.Parameters.Count < 2)
                {
                    player.SendMessage(
                        $"Usage: /class choose <class>\nAvailable classes:\n{string.Join("\n", classChoices.Select(classname => $"-{classname}"))}",
                        Color.LightCyan
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

                player.SendSuccessMessage(
                    $"You are now a {classinput.ToUpper()} class!",
                    Color.Aquamarine
                );
            }
            else if (args.Parameters[0] == "show")
            {
                player.SendErrorMessage("Soon to be implemented...");
            }
        }

        // Utility
        private void SendMessage(TSPlayer player, string message, Color color)
        {
            if (
                !LastMessageSent.ContainsKey(player.Name)
                || (DateTime.Now - LastMessageSent[player.Name]).Seconds
                    >= Config.ErrorMessageInterval
            )
            {
                LastMessageSent[player.Name] = DateTime.Now;
                player.SendMessage(message, color);
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
