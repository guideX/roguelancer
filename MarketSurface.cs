namespace Roguelancer;

/// <summary>
/// Identifies the policy surface through which a station listing is exposed.
/// Both surfaces share the same MarketManager runtime listing and stock.
/// </summary>
public enum MarketSurface
{
    Ordinary,
    BlackMarket
}
