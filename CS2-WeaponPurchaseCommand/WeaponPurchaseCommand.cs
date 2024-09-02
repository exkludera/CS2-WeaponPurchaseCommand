using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;

namespace WeaponPurchaseCommand;

public class PurchaseHistory
{
    public Dictionary<string, int> PlayerBuyHistory { get; set; } = new Dictionary<string, int>();
    public bool IsCooldownNow { get; set; } = false;
}

public class WeaponPurchaseCommand : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "CS2-Weapon Purchase Command";
    public override string ModuleVersion => "1.3";
    public override string ModuleAuthor => "Oylsister, updated by exkludera";
    public override string ModuleDescription => "Purchase weapon command for counter-strike 2";

    public Dictionary<CCSPlayerController, PurchaseHistory> PlayerBuyList { get; set; } = new();

    public override void Load(bool hotReload)
    {
        AddTimer(1.0f, () =>
        {
            AddCommand("css_weaponlist", "Command Check Weapon list", CheckWeaponListCommand);

            RegisterListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
            RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);

            foreach (var weapon in Config.WeaponConfigs)
            {
                foreach (var command in weapon.Value.PurchaseCommand)
                {
                    AddCommand(command, "Buy Command", PurchaseWeaponCommand);
                }
            }

            MemoryFunctionWithReturn<CCSPlayer_WeaponServices, CBasePlayerWeapon, bool> CCSPlayer_WeaponServices_CanUseFunc = new(GameData.GetSignature("CCSPlayer_WeaponServices_CanUse"));
            CCSPlayer_WeaponServices_CanUseFunc.Hook(OnWeaponCanUse, HookMode.Pre);

            foreach (var player in Utilities.GetPlayers().Where(p => !p.IsBot))
            {
                if (!PlayerBuyList.ContainsKey(player))
                    PlayerBuyList.Add(player, new PurchaseHistory());
            }
        });
    }

    public override void Unload(bool hotReload)
    {
        RemoveCommand("css_weaponlist", CheckWeaponListCommand);

        RemoveListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
        RemoveListener<Listeners.OnClientDisconnect>(OnClientDisconnect);

        foreach (var weapon in Config.WeaponConfigs)
        {
            foreach (var command in weapon.Value.PurchaseCommand)
            {
                RemoveCommand(command, PurchaseWeaponCommand);
            }
        }

        MemoryFunctionWithReturn<CCSPlayer_WeaponServices, CBasePlayerWeapon, bool> CCSPlayer_WeaponServices_CanUseFunc = new(GameData.GetSignature("CCSPlayer_WeaponServices_CanUse"));
        CCSPlayer_WeaponServices_CanUseFunc.Unhook(OnWeaponCanUse, HookMode.Pre);

        PlayerBuyList.Clear();
    }

    public Config Config { get; set; } = new Config();
    public void OnConfigParsed(Config config)
    {
        Config = config;
        Config.Prefix = StringExtensions.ReplaceColorTags(config.Prefix);
    }

    public void OnClientPutInServer(int playerSlot)
    {
        var client = Utilities.GetPlayerFromSlot(playerSlot);

        if (client == null || !client.IsValid || client.IsBot)
            return;

        if (!PlayerBuyList.ContainsKey(client))
            PlayerBuyList.Add(client, new PurchaseHistory());
    }

    public void OnClientDisconnect(int playerSlot)
    {
        var client = Utilities.GetPlayerFromSlot(playerSlot);

        if (client == null || !client.IsValid ||client.IsBot)
            return;

        if (PlayerBuyList.ContainsKey(client))
            PlayerBuyList.Remove(client);
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var client = @event.Userid;

        if (client == null || !client.IsValid || client.IsBot)
            return HookResult.Continue;

        if (PlayerBuyList.ContainsKey(client))
            PlayerBuyList[client].PlayerBuyHistory.Clear();

        return HookResult.Continue;
    }

    public void CheckWeaponListCommand(CCSPlayerController? client, CommandInfo info)
    {
        if (client == null)
            return;

        foreach (string KeyValue in Config.WeaponConfigs.Keys)
        {
            client.PrintToChat($"Weapon: {KeyValue}");

            string commandlist = "";

            foreach (string commandValue in Config.WeaponConfigs[KeyValue].PurchaseCommand)
                commandlist += commandValue + ",";

            client.PrintToChat($"Command: {commandlist}");
        }
    }

    public void PurchaseWeaponCommand(CCSPlayerController? client, CommandInfo info)
    {
        var weaponCommand = info.GetArg(0);

        foreach (string keyVar in Config.WeaponConfigs.Keys)
        {
            foreach (string command in Config.WeaponConfigs[keyVar].PurchaseCommand)
            {
                if (weaponCommand == command)
                {
                    PurchaseWeapon(client!, keyVar);
                    break;
                }
            }
        }
    }

    public void PurchaseWeapon(CCSPlayerController client, string weapon)
    {
        var weaponConfig = Config.WeaponConfigs[weapon];

        if (weaponConfig == null)
        {
            client.PrintToChat(Config.Prefix + Localizer["Invalid Weapon"]);
            return;
        }

        if (!client.PawnIsAlive)
        {
            client.PrintToChat(Config.Prefix + Localizer["Not Alive"]);
            return;
        }

        if (weaponConfig.PurchaseRestrict)
        {
            client.PrintToChat(Config.Prefix + Localizer["Weapon Restricted", weapon]);
            return;
        }

        var cooldown = Config.CooldownPurchase;
        if (cooldown > 0 && PlayerBuyList[client].IsCooldownNow)
        {
            client.PrintToChat(Config.Prefix + Localizer["Purchase Cooldown"]);
            return;
        }

        var clientMoney = client.InGameMoneyServices!.Account;

        if (clientMoney < weaponConfig.PurchasePrice)
        {
            client.PrintToChat(Config.Prefix + Localizer["Not Enough Money"]);
            return;
        }

        int weaponPurchased;
        bool weaponFound = PlayerBuyList[client].PlayerBuyHistory.TryGetValue(weapon, out weaponPurchased);

        if (weaponConfig.PurchaseLimit > 0)
        {
            if (weaponFound)
            {
                if (weaponPurchased >= weaponConfig.PurchaseLimit)
                {
                    client.PrintToChat(Config.Prefix + Localizer["Maximum Purchase", weapon]);
                    return;
                }
                else
                {
                    PlayerBuyList[client].PlayerBuyHistory[weapon] = weaponPurchased + 1;
                }
            }
            else
            {
                PlayerBuyList[client].PlayerBuyHistory.Add(weapon, 1);
            }

            client.PrintToChat(Config.Prefix + Localizer["Purchase Limit", weapon, weaponConfig.PurchaseLimit - weaponPurchased - 1, weaponConfig.PurchaseLimit]);
        }

        else
        {
            client.PrintToChat(Config.Prefix + Localizer["Purchased", weapon]);
        }

        client.ExecuteClientCommand("slot3");
        client.ExecuteClientCommand($"slot{weaponConfig.WeaponSlot + 1}");

        var activeweapon = client.PlayerPawn.Value?.WeaponServices?.ActiveWeapon;

        if (activeweapon != null && activeweapon.Value?.DesignerName != "weapon_knife")
            client.DropActiveWeapon();

        client.GiveNamedItem(weaponConfig.WeaponEntity!);

        client.InGameMoneyServices.Account = clientMoney - weaponConfig.PurchasePrice;
        Utilities.SetStateChanged(client, "CCSPlayerController", "m_pInGameMoneyServices");

        AddTimer(0.2f, () =>
        {
            client.ExecuteClientCommand($"slot{weaponConfig.WeaponSlot + 1}");
        });

        PlayerBuyList[client].IsCooldownNow = true;


        AddTimer(cooldown, () =>
        {
            PlayerBuyList[client].IsCooldownNow = false;
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
        if (!Config.WeaponConfigs.ContainsKey(weaponKey))
            return false;

        return Config.WeaponConfigs[weaponKey].PurchaseRestrict;
    }

    public string GetWeaponKeyByEntityName(string entityName)
    {
        foreach (string keyVar in Config.WeaponConfigs.Keys)
        {
            if (entityName == Config.WeaponConfigs[keyVar].WeaponEntity)
                return keyVar;
        }
        return "none";
    }
}