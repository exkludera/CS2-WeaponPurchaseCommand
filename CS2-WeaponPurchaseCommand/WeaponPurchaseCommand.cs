using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace WeaponPurchaseCommand
{
    public class WeaponPurchaseCommand : BasePlugin
    {
        public override string ModuleName => "CS2-Weapon Purchase Command";
        public override string ModuleVersion => "1.1";
        public override string ModuleAuthor => "Oylsister";
        public override string ModuleDescription => "Purchase weapon command for counter-strike 2";

        public ConfigFile? PurchaseConfig { get; private set; }

        public bool ConfigIsLoaded { get; set; } = false;
        public bool CommandAssigned { get; set; } = false;

        public Dictionary<int, PurchaseHistory> PlayerBuyList { get; set; } = new();

        public override void Load(bool hotReload)
        {
            AddCommand("css_weaponlist", "Command Check Weapon list", CheckWeaponListCommand);
            RegisterListener<Listeners.OnMapStart>(OnMapStart);
            RegisterListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
            RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
        }

        public void OnMapStart(string mapname)
        {
            var configPath = Path.Combine(ModuleDirectory, "weapons.json");

            if (!File.Exists(configPath))
            {
                Logger.LogError("There is no config");
                ConfigIsLoaded = false;
                return;
            }

            PurchaseConfig = JsonConvert.DeserializeObject<ConfigFile>(File.ReadAllText(configPath));
            Logger.LogInformation("Weapon Config is loaded");
            ConfigIsLoaded = true;

            InitialCommand();

            MemoryFunctionWithReturn<CCSPlayer_WeaponServices, CBasePlayerWeapon, bool> CCSPlayer_WeaponServices_CanUseFunc = new(GameData.GetSignature("CCSPlayer_WeaponServices_CanUse"));
            CCSPlayer_WeaponServices_CanUseFunc.Hook(OnWeaponCanUse, HookMode.Pre);
        }

        public void OnClientPutInServer(int playerSlot)
        {
            var client = Utilities.GetPlayerFromSlot(playerSlot);

            PlayerBuyList.Add(client.Slot, new PurchaseHistory());
        }

        public void OnClientDisconnect(int playerSlot)
        {
            var client = Utilities.GetPlayerFromSlot(playerSlot);

            PlayerBuyList.Remove(client.Slot);
        }

        [GameEventHandler]
        public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            var client = @event.Userid;

            if (PlayerBuyList.ContainsKey(client.Slot))
                PlayerBuyList[client.Slot].PlayerBuyHistory.Clear();

            return HookResult.Continue;
        }

        public void CheckWeaponListCommand(CCSPlayerController? client, CommandInfo info)
        {
            if (client == null)
            {
                return;
            }

            foreach (string KeyValue in PurchaseConfig!.WeaponConfigs.Keys)
            {
                client.PrintToChat($"Key: {KeyValue}");

                string commandlist = "";

                foreach (string commandValue in PurchaseConfig.WeaponConfigs[KeyValue].PurchaseCommand)
                {
                    commandlist += commandValue + ",";
                }
                client.PrintToChat($"Command: {commandlist}");
            }
        }

        private void InitialCommand()
        {
            if (CommandAssigned)
            {
                return;
            }

            if (PurchaseConfig == null)
                return;

            foreach (var weapon in PurchaseConfig.WeaponConfigs)
            {
                foreach (var command in weapon.Value.PurchaseCommand)
                {
                    AddCommand(command, "Buy Command", PurchaseWeaponCommand);
                }
            }

            CommandAssigned = true;
        }

        public void PurchaseWeaponCommand(CCSPlayerController? client, CommandInfo info)
        {
            var weaponCommand = info.GetArg(0);

            foreach (string keyVar in PurchaseConfig!.WeaponConfigs.Keys)
            {
                foreach (string command in PurchaseConfig.WeaponConfigs[keyVar].PurchaseCommand)
                {
                    if (weaponCommand == command)
                    {
                        // info.ReplyToCommand($"{weaponCommand} with {command}");
                        PurchaseWeapon(client!, keyVar);
                        break;
                    }
                }
            }
        }

        public void PurchaseWeapon(CCSPlayerController client, string weapon)
        {
            var weaponConfig = PurchaseConfig!.WeaponConfigs[weapon];

            if (weaponConfig == null)
            {
                client.PrintToChat($" {ChatColors.Green}[Weapon]{ChatColors.Default} Invalid weapon!");
                return;
            }

            if (!client.PawnIsAlive)
            {
                client.PrintToChat($" {ChatColors.Green}[Weapon]{ChatColors.Default} this feature need you to be alive!");
                return;
            }

            if (weaponConfig.PurchaseRestrict)
            {
                client.PrintToChat($" {ChatColors.Green}[Weapon]{ChatColors.Default} Weapon {ChatColors.Lime}{weapon}{ChatColors.Default} is restricted");
                return;
            }

            var cooldown = PurchaseConfig!.CooldownPurchase;
            if (cooldown > 0 && PlayerBuyList[client.Slot].IsCooldownNow)
            {
                client.PrintToChat($" {ChatColors.Green}[Weapon]{ChatColors.Default} Your purchase is on cooldown now!");
                return;
            }

            var clientMoney = client.InGameMoneyServices!.Account;

            if (clientMoney < weaponConfig.PurchasePrice)
            {
                client.PrintToChat($" {ChatColors.Green}[Weapon]{ChatColors.Default} You don't have enough money to purchase this weapon!");
                return;
            }

            int weaponPurchased;
            bool weaponFound = PlayerBuyList[client.Slot].PlayerBuyHistory.TryGetValue(weapon, out weaponPurchased);

            if (weaponConfig.PurchaseLimit > 0)
            {
                if (weaponFound)
                {
                    if (weaponPurchased >= weaponConfig.PurchaseLimit)
                    {
                        client.PrintToChat($" {ChatColors.Green}[Weapon]{ChatColors.Default} You have reached maximum purchase for {ChatColors.Lime}{weapon}{ChatColors.Default}, you can purchase again in next round");
                        return;
                    }
                    else
                    {
                        //client.PrintToChat($"{ChatColors.Lime}{weapon}{ChatColors.Default} Purchased: {weaponPurchased}");
                        PlayerBuyList[client.Slot].PlayerBuyHistory[weapon] = weaponPurchased + 1;
                    }
                }
                else
                {
                    PlayerBuyList[client.Slot].PlayerBuyHistory.Add(weapon, 1);
                }

                client.PrintToChat($" {ChatColors.Green}[Weapon]{ChatColors.Default} You have purchase {ChatColors.Lime}{weapon}{ChatColors.Default}, Purchase Limit: {ChatColors.Green}{weaponConfig.PurchaseLimit - weaponPurchased - 1}/{weaponConfig.PurchaseLimit}");
            }

            else
            {
                client.PrintToChat($" {ChatColors.Green}[Weapon]{ChatColors.Default} You have purchase {ChatColors.Lime}{weapon}{ChatColors.Default}.");
            }

            client.ExecuteClientCommand("slot3");
            client.ExecuteClientCommand($"slot{weaponConfig.WeaponSlot + 1}");

            var activeweapon = client.PlayerPawn.Value!.WeaponServices!.ActiveWeapon;

            if (activeweapon != null && activeweapon.Value!.DesignerName != "weapon_knife")
                client.DropActiveWeapon();

            client.GiveNamedItem(weaponConfig.WeaponEntity!);
            client.InGameMoneyServices!.Account -= weaponConfig.PurchasePrice;

            AddTimer(0.2f, () =>
            {
                client.ExecuteClientCommand($"slot{weaponConfig.WeaponSlot + 1}");
            });

            PlayerBuyList[client.Slot].IsCooldownNow = true;


            AddTimer(cooldown, () =>
            {
                PlayerBuyList[client.Slot].IsCooldownNow = false;
            });
        }
        private HookResult OnWeaponCanUse(DynamicHook hook)
        {
            var weapon = hook.GetParam<CBasePlayerWeapon>(1);

            var weaponKey = GetWeaponKeyByEntityName(weapon.DesignerName);

            if (IsWeaponRestricted(weaponKey))
            {
                hook.SetReturn(false);
                return HookResult.Handled;
            }

            return HookResult.Continue;
        }

        public bool IsWeaponRestricted(string weaponKey)
        {
            if (!PurchaseConfig!.WeaponConfigs.ContainsKey(weaponKey))
                return false;

            return PurchaseConfig!.WeaponConfigs[weaponKey].PurchaseRestrict;
        }

        public string GetWeaponKeyByEntityName(string entityName)
        {
            foreach (string keyVar in PurchaseConfig!.WeaponConfigs.Keys)
            {
                if (entityName == PurchaseConfig!.WeaponConfigs[keyVar].WeaponEntity)
                    return keyVar;
            }
            return "none";
        }
    }
}

public class ConfigFile
{
    public float CooldownPurchase { get; set; } = 0f;

    public Dictionary<string, WeaponConfig> WeaponConfigs { get; set; } = new Dictionary<string, WeaponConfig>();

    public ConfigFile()
    {
        List<string> commandlist = new List<string> { "css_glock" };
        WeaponConfigs = new Dictionary<string, WeaponConfig>(StringComparer.OrdinalIgnoreCase)
        {
            { "Glock", new WeaponConfig(commandlist, "weapon_glock", 1, 100, 0, true) },
        };
    }
}

public class PurchaseHistory
{
    public Dictionary<string, int> PlayerBuyHistory { get; set; } = new Dictionary<string, int>();
    public bool IsCooldownNow { get; set; } = false;
}

public class WeaponConfig
{
    public WeaponConfig(List<string> purchaseCommand, string weaponEntity, int slot, int price, int limitbuy, bool restrict)
    {
        PurchaseCommand = purchaseCommand;
        WeaponEntity = weaponEntity;
        WeaponSlot = slot;
        PurchasePrice = price;
        PurchaseLimit = limitbuy;
        PurchaseRestrict = restrict;
    }

    public List<string> PurchaseCommand { get; set; } = new List<string>();
    public string? WeaponEntity { get; set; }
    public int WeaponSlot { get; set; }
    public int PurchasePrice { get; set; }
    public int PurchaseLimit { get; set; } = 0;
    public bool PurchaseRestrict { get; set; }
}