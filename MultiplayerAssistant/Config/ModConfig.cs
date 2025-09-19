using System.Collections.Generic;

namespace MultiplayerAssistant.Config
{
    public class ModConfig
    {
        public string FarmName { get; set; } = "Stardew";

        // Options are 0, 1, 2, or 3.
        // 说明：游戏本体对开局小屋有 0~3 的硬限制。
        // 如需“默认总计 8 间”，这里设为 3，并配合 ExtraCabins 默认值 5 实现。
        public int StartingCabins { get; set; } = 3;

        // Options are "nearby" or "separate"
        public string CabinLayout { get; set; } = "separate";
        
        // Options are "normal", "75%", "50%", or "25%"
        public string ProfitMargin { get; set; } = "normal";

        // Options are "shared" or "separate"
        public string MoneyStyle { get; set; } = "shared";

        // Options are "standard", "riverland", "forest", "hilltop", "wilderness", "fourcorners", "beach".
        public string FarmType { get; set; } = "standard";

        // Options are "normal" or "remixed".
        public string CommunityCenterBundles { get; set; } = "normal";
        
        public bool GuaranteeYear1Completable { get; set; } = false;

        // Options are "normal" or "remixed".
        public string MineRewards { get; set; } = "normal";

        public bool SpawnMonstersOnFarmAtNight { get; set; } = false;

        public ulong? RandomSeed { get; set; } = null;

        public bool AcceptPet = true; // By default, accept the pet (of course).
        
        // Nullable. Must not be null if AcceptPet is true. Options are "dog" or "cat".
        public string PetSpecies { get; set; } = "dog";

        // Nullable. Must not be null if AcceptPet is true. Options are 0, 1, or 2.
        public int? PetBreed { get; set; } = 0;

        // Nullable. Must not be null if AcceptPet is true. Any string.
        public string PetName { get; set; } = "Stella";

        // Options are "Mushrooms" or "Bats" (case-insensitive)
        public string MushroomsOrBats { get; set; } = "Mushrooms";

        // Enables the crop saver
        public bool EnableCropSaver = true;

        // Configures the automated host to purchase a Joja membership once available,
        // committing to the Joja route and removing the community center.
        public bool PurchaseJojaMembership = false;

        // Changes farmhands permissions to move buildings from the Carpenter's Shop.
        // Is set each time the server is started and can be changed in the game.
        // "off" to entirely disable moving buildings.
        // "owned" to allow farmhands to move buildings that they purchased.
        // "on" to allow moving all buildings.
        public string MoveBuildPermission { get; set; } = "off";

        // Auto sleep enhancements
        // Time of day (e.g. 2200 for 10PM) when host auto-sleeps if players are online.
        public int AutoSleepTime { get; set; } = 2200;
        // If true, the host will auto-return to bed location before sleeping.
        public bool ForceReturnToBed { get; set; } = true;

        // Festival warnings / automation (lightweight)
        // Seconds of real-time delay after festival start before auto prompts are sent.
        public int FestivalAutoStartDelaySeconds { get; set; } = 60;
        // Seconds before festival end to warn players (chat broadcast). Best-effort based on in-game time.
        public int FestivalEndWarningSeconds { get; set; } = 120;
        // Seconds after festival opens to force-start automatically (players can PM cancel)
        public int FestivalForceStartSeconds { get; set; } = 60;

        // ------------------ Enhancements ------------------
        // Player limit for server (<=0 to use maximum)
        public int PlayerLimit { get; set; } = 0;

        // Extra cabins management
        // number of extra cabins to create after save load
        // 默认 5，以配合 StartingCabins=3 达到“开服总计约 8 间”的效果。
        public int ExtraCabins { get; set; } = 5;
        // stone / plank / log
        public string CabinStyle { get; set; } = "stone";
        // Optional explicit cabin positions (tile coords). If provided, positions are attempted first.
        public List<Position> CabinPositions { get; set; } = new List<Position>();

        // Starting gold for a newly created farm
        public int StartGold { get; set; } = 0;

        // Starting skills (0-10) for newly created farm
        public StartSkillsConfig StartSkills { get; set; } = new StartSkillsConfig();

        // Mail flags to add for tomorrow on a newly created farm
        public List<string> StartMail { get; set; } = new List<string>();

        // Desired weather for tomorrow on day 1 (sunny|rain|storm|wind). Empty = unchanged.
        public string StartWeatherDay1 { get; set; } = "";

        // Lock moving buildings after startup; prevents in-game changes via chat command.
        public bool LockCabinMove { get; set; } = false;

        // ------------------ Chest lock (cabin owner) ------------------
        // Master switch
        public bool ChestLockEnabled { get; set; } = false;
        // Scope: placed-by-owner | inside | nearby
        public string ChestLockScope { get; set; } = "placed-by-owner";
        // Host bypasses all locks
        public bool ChestLockHostBypass { get; set; } = true;
        // Whitelist of players (Name or UniqueMultiplayerID string) who can open any locked chest
        public List<string> ChestLockWhitelist { get; set; } = new List<string>();
        // Allow owner to toggle chest as public via chat command
        public bool ChestLockAllowPublicToggle { get; set; } = true;
        // Radius for 'nearby' scope around cabin building (tiles)
        public int ChestLockNearbyRadius { get; set; } = 5;

        // ------------------ Invite code sync ------------------
        public bool WriteInviteCodeFile { get; set; } = true;
        public string InviteCodeFileName { get; set; } = "InviteCode.txt";

        // ------------------ Relationship protection ------------------
        // Prevent host's friendship points from decaying while server runs
        public bool FreezeHostHeartLoss { get; set; } = true;

        // ------------------ Joja auto-upgrades ------------------
        public bool JojaAutoUpgradesEnabled { get; set; } = false;
        // Purchase only when money >= cost * multiplier (default 2x)
        public float JojaUpgradeBudgetMultiplier { get; set; } = 2.0f;
        // Purchase order (keys: minecarts, quarry, boulder, greenhouse, bus)
        public List<string> JojaUpgradeOrder { get; set; } = new List<string> { "minecarts", "quarry", "boulder", "greenhouse", "bus" };
        // Costs (g)
        public MultiplayerAssistant.Utils.SerializableDictionary<string, int> JojaUpgradeCosts { get; set; } = new MultiplayerAssistant.Utils.SerializableDictionary<string, int>
        {
            { "minecarts", 15000 },
            { "boulder", 20000 },
            { "quarry", 25000 },
            { "greenhouse", 35000 },
            { "bus", 40000 }
        };
        // Seconds to announce before purchasing; players can PM 'cancel' to ServerBot to abort the pending buy.
        public int JojaPurchaseConfirmDelaySeconds { get; set; } = 10;

        // ------------------ Anti-cheat (lightweight) ------------------
        // Sanitize names entered in NamingMenu (animals/pets/children) to block classic spawn codes
        public bool AntiCheatSanitizeNames { get; set; } = true;
        // Regular expressions blocked (remove all matches)
        public System.Collections.Generic.List<string> AntiCheatBlockedNameRegex { get; set; } = new System.Collections.Generic.List<string>
        {
            @"\[[0-9]+\]",        // old bracket spawn codes like [74]
            @"\((O|P)\)[0-9]+"     // object/parent sheet codes like (O)74
        };
        // If true, announce when sanitized
        public bool AntiCheatAnnounceSanitize { get; set; } = false;

        public class Position
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        public class StartSkillsConfig
        {
            public int Farming { get; set; } = 0;
            public int Fishing { get; set; } = 0;
            public int Foraging { get; set; } = 0;
            public int Mining { get; set; } = 0;
            public int Combat { get; set; } = 0;
        }
    }
}
