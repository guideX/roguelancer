using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Read-only cargo view shared by player reconnaissance and authoritative
/// cargo owners. It contains canonical commodity definitions, never raw IDs
/// alone, and is always a snapshot rather than a mutable cargo authority.
/// </summary>
public sealed class NpcCargoManifestStackSnapshot
{
    public Commodity Commodity { get; }
    public int Quantity { get; }
    public bool IsStolen { get; }

    public NpcCargoManifestStackSnapshot(Commodity commodity, int quantity, bool isStolen = false)
    {
        Commodity = commodity;
        Quantity = Math.Max(0, quantity);
        IsStolen = isStolen;
    }
}

public sealed class NpcCargoManifestSnapshot
{
    private readonly List<NpcCargoManifestStackSnapshot> _stacks;

    public bool HasRegisteredCargo { get; }
    public IReadOnlyList<NpcCargoManifestStackSnapshot> Stacks => _stacks;
    public int RemainingQuantity => _stacks.Sum(stack => Math.Max(0, stack.Quantity));

    public NpcCargoManifestSnapshot(
        bool hasRegisteredCargo,
        IEnumerable<NpcCargoManifestStackSnapshot> stacks = null)
    {
        HasRegisteredCargo = hasRegisteredCargo;
        _stacks = stacks?
            .Where(stack => stack?.Commodity != null && stack.Quantity > 0)
            .Select(stack => new NpcCargoManifestStackSnapshot(stack.Commodity, stack.Quantity, stack.IsStolen))
            .ToList()
            ?? new List<NpcCargoManifestStackSnapshot>();
    }

    public static NpcCargoManifestSnapshot NoRegisteredCargo() =>
        new(false);
}
