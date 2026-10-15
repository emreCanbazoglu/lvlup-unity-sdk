namespace LvlUp.Models
{
    /// <summary>Kind of resource held in a player's wallet.</summary>
    public enum ResourceType
    {
        Currency,
        Booster,
        Consumable,
        Other
    }

    internal static class ResourceTypeExtensions
    {
        internal static string ToWireString(this ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Currency: return "currency";
                case ResourceType.Booster: return "booster";
                case ResourceType.Consumable: return "consumable";
                default: return "other";
            }
        }
    }
}
