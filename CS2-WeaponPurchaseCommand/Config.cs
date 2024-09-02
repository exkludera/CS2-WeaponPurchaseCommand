using CounterStrikeSharp.API.Core;

public class Config : BasePluginConfig
{
    public string Prefix { get; set; } = "{green}[Weapon]{default}";
    public float CooldownPurchase { get; set; } = 0f;
    public Dictionary<string, WeaponConfig> WeaponConfigs { get; set; } = new Dictionary<string, WeaponConfig>
        {
            { "AK47", new WeaponConfig(new List<string> { "css_ak", "css_ak47" }, "weapon_ak47", 0, 2700, 1, true) }
        };
}

public class WeaponConfig
{
    public WeaponConfig(List<string> purchaseCommand, string weaponEntity, int weaponSlot, int purchasePrice, int purchaseLimit, bool purchaseRestrict)
    {
        PurchaseCommand = purchaseCommand;
        WeaponEntity = weaponEntity;
        WeaponSlot = weaponSlot;
        PurchasePrice = purchasePrice;
        PurchaseLimit = purchaseLimit;
        PurchaseRestrict = purchaseRestrict;
    }

    public List<string> PurchaseCommand { get; set; } = new List<string>();
    public string? WeaponEntity { get; set; }
    public int WeaponSlot { get; set; }
    public int PurchasePrice { get; set; }
    public int PurchaseLimit { get; set; } = 0;
    public bool PurchaseRestrict { get; set; }
}