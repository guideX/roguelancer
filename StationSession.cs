using Microsoft.Xna.Framework;

namespace Roguelancer;

/// <summary>
/// Identifies why the generic station interior is active.
/// </summary>
public enum StationSessionEntryMode
{
    DeveloperTest,
    RealDocked
}

/// <summary>
/// Bounded context for one on-foot station visit. The player's ship remains
/// authoritative in space; the bay uses only the display transform supplied by
/// StationTestScene while this context is active.
/// </summary>
public sealed class StationSession
{
    private StationSession(
        StationSessionEntryMode entryMode,
        Station dockedStation,
        Ship playerShip,
        int systemIndex,
        Vector3 dockingSpacePosition,
        Vector3 dockingSpaceForward,
        Vector3 dockingPoint,
        Vector3 launchPosition,
        Vector3 launchForward)
    {
        EntryMode = entryMode;
        DockedStation = dockedStation;
        PlayerShip = playerShip;
        SystemIndex = systemIndex;
        DockingSpacePosition = dockingSpacePosition;
        DockingSpaceForward = dockingSpaceForward;
        DockingPoint = dockingPoint;
        LaunchPosition = launchPosition;
        LaunchForward = launchForward;
    }

    public StationSessionEntryMode EntryMode { get; }
    public bool IsDeveloperTest => EntryMode == StationSessionEntryMode.DeveloperTest;
    public bool IsRealDockedSession => EntryMode == StationSessionEntryMode.RealDocked;
    public Station DockedStation { get; }
    public Ship PlayerShip { get; }
    public int SystemIndex { get; }
    public Vector3 DockingSpacePosition { get; }
    public Vector3 DockingSpaceForward { get; }
    public Vector3 DockingPoint { get; }
    public Vector3 LaunchPosition { get; }
    public Vector3 LaunchForward { get; }

    public string StationDisplayName => DockedStation?.Name ?? "Station Test Bay";

    public static StationSession CreateDeveloperTest(Ship playerShip)
    {
        return new StationSession(
            StationSessionEntryMode.DeveloperTest,
            null,
            playerShip,
            0,
            playerShip?.Position ?? Vector3.Zero,
            playerShip?.Forward ?? Vector3.Forward,
            Vector3.Zero,
            playerShip?.Position ?? Vector3.Zero,
            playerShip?.Forward ?? Vector3.Forward);
    }

    public static StationSession CreateRealDocked(Station station, Ship playerShip, int currentSystemIndex)
    {
        Vector3 dockingPoint = station?.GetDockingPoint() ?? playerShip?.Position ?? Vector3.Zero;
        Vector3 shipPosition = playerShip?.Position ?? dockingPoint;
        Vector3 dockingForward = SafeNormalize(playerShip?.Forward ?? Vector3.Forward, Vector3.Forward);

        // The autopilot approaches the port from outside the station. Preserve
        // that outward direction for launch, even when a station's configured
        // docking point is at its center and has no usable radial vector.
        Vector3 launchForward = dockingPoint - (station?.Position ?? dockingPoint);
        if (launchForward.LengthSquared() < 0.01f)
        {
            launchForward = -dockingForward;
        }
        launchForward = SafeNormalize(launchForward, -dockingForward);

        float stationClearance = station?.Radius ?? 200f;
        float approachClearance = station?.Config?.DockingApproachDistance ?? 200f;
        float dockRangeClearance = (station?.DockingRange ?? 500f) + 150f;
        float launchDistance = MathHelper.Max(
            stationClearance + (playerShip?.CollisionRadius ?? 10f) + 250f,
            MathHelper.Max(approachClearance + 100f, dockRangeClearance));
        Vector3 launchPosition = dockingPoint + launchForward * launchDistance;

        return new StationSession(
            StationSessionEntryMode.RealDocked,
            station,
            playerShip,
            station?.Config?.SystemIndex > 0 ? station.Config.SystemIndex : currentSystemIndex,
            shipPosition,
            dockingForward,
            dockingPoint,
            launchPosition,
            launchForward);
    }

    private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
    {
        return value.LengthSquared() > 0.0001f ? Vector3.Normalize(value) : fallback;
    }
}
