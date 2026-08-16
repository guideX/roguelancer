using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Roguelancer;

/// <summary>
/// Headless validation for the Phase 10 room/door/social contract. Graphics
/// loading is intentionally not required, so this can run on a remote machine
/// before a manual walk-through is available.
/// </summary>
internal sealed class StationBarSocialSmokeTest
{
    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase(ValidateLayout, "compact connected bar layout", ref passed, ref failed);
        RunCase(ValidateDoorTraversal, "door collision follows state", ref passed, ref failed);
        RunCase(ValidateSocialRoles, "bartender and three patron roles", ref passed, ref failed);
        RunCase(ValidateDialoguePayloads, "speaker and bounded dialogue payloads", ref passed, ref failed);
        RunCase(ValidateSingleInteractionResolution, "one-target social interaction resolution", ref passed, ref failed);
        RunCase(ValidateMissionBoardPlacement, "mission board placement and signage", ref passed, ref failed);
        Console.WriteLine($"[BAR SOCIAL SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private static void RunCase(Func<(bool Success, string FailureReason)> test, string label, ref int passed, ref int failed)
    {
        try
        {
            (bool success, string reason) = test();
            if (success)
            {
                passed++;
                Console.WriteLine($"[BAR SOCIAL SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[BAR SOCIAL SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[BAR SOCIAL SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private static (bool, string) ValidateLayout()
    {
        StationBarLayout layout = StationBarSocial.Layout;
        if (!layout.IsCompactSocialRoom) return Fail("layout is outside the compact bar target");
        if (layout.DoorPosition.Z >= layout.Minimum.Z) return Fail("bar portal is not connected from the concourse side");
        if (layout.DoorPosition.X < layout.Minimum.X || layout.DoorPosition.X > layout.Maximum.X)
            return Fail("bar portal is outside the social room width");
        return Pass();
    }

    private static (bool, string) ValidateDoorTraversal()
    {
        StationDoorController door = new();
        if (door.State != StationDoorState.Closed || !door.BlocksTraversal) return Fail("door did not initialize closed and blocking");
        if (door.ActionLabel != "Press E to enter") return Fail("closed door prompt was incorrect");
        if (!door.TryOpen() || door.State != StationDoorState.Opening) return Fail("door did not enter opening state");
        if (door.ActionLabel != "Opening...") return Fail("opening prompt was not bounded");
        door.Update(0.4f);
        if (!door.BlocksTraversal) return Fail("door cleared collision before the safe traversal threshold");
        door.Update(0.4f);
        if (!door.IsOpen || door.BlocksTraversal) return Fail("open door did not clear traversal collision");
        door.Reset();
        if (door.State != StationDoorState.Closed || !door.BlocksTraversal) return Fail("door reset did not restore blocking state");
        return Pass();
    }

    private static (bool, string) ValidateSocialRoles()
    {
        var roles = StationBarSocial.CreateRoles();
        if (roles.Count != 4) return Fail($"expected four social roles, found {roles.Count}");
        if (!roles.Any(role => role.Id == "bartender") || roles.Count(role => role.Id != "bartender") != 3)
            return Fail("bartender/patron role split was incorrect");
        StationSocialNpcDefinition bartender = roles.Single(role => role.Id == "bartender");
        if (bartender.InteractionRadius < 3.0f) return Fail("bartender customer interaction zone is too small for the counter");
        if (roles.Any(role => role.DisplayName.Contains("Adam", StringComparison.OrdinalIgnoreCase)))
            return Fail("temporary asset name leaked into gameplay identity");
        if (roles.Select(role => role.IdleOffset).Distinct().Count() != roles.Count)
            return Fail("social roles do not have independent idle offsets");
        if (roles.Any(role => role.InteractionRadius <= 0.0f)) return Fail("social interaction radius was not configured");
        return Pass();
    }

    private static (bool, string) ValidateDialoguePayloads()
    {
        var roles = StationBarSocial.CreateRoles();
        StationSocialNpcDefinition bartender = roles.Single(role => role.Id == "bartender");
        foreach (StationSocialNpcDefinition role in roles)
        {
            if (role.Dialogue.Speaker != role.DisplayName || string.IsNullOrWhiteSpace(role.Dialogue.Text))
                return Fail($"dialogue payload is incomplete for {role.DisplayName}");
            if (role.Dialogue.Duration <= 0.0f || role.Dialogue.Duration > 10.0f)
                return Fail($"dialogue duration is not bounded for {role.DisplayName}");
        }
        if (!bartender.HasFutureMissionHook || bartender.InteractionRole != StationNpcInteractionRole.Dialogue)
            return Fail("bartender future mission hook was not structural-only");
        return Pass();
    }

    private static (bool, string) ValidateSingleInteractionResolution()
    {
        List<StationInteraction> interactions = new();
        foreach (StationSocialNpcDefinition role in StationBarSocial.CreateRoles())
        {
            interactions.Add(new StationInteraction(
                $"npc-{role.Id}",
                role.Position,
                role.InteractionRadius,
                role.DisplayName.ToUpperInvariant(),
                "Press E to talk",
                () => { }));
        }

        StationInteraction? nearest = StationInteractionResolver.FindNearest(
            interactions,
            new Vector3(-4.15f, 0.0f, 57.6f),
            Vector3.Forward);
        if (nearest == null || nearest.Id != "npc-smuggler") return Fail("nearest social target was not selected");
        if (interactions.Count != 4) return Fail("social target list did not remain one target per NPC");
        return Pass();
    }

    private static (bool, string) ValidateMissionBoardPlacement()
    {
        StationTestScene scene = new();
        StationBarLayout layout = StationBarSocial.Layout;
        Vector3 board = scene.MissionBoardInteractionPosition;
        if (scene.MissionBoardSignText != "MISSION BOARD")
            return Fail("mission board signage text was not configured");
        if (board.X < layout.Minimum.X || board.X > layout.Maximum.X ||
            board.Z < layout.Minimum.Z || board.Z > layout.Maximum.Z)
            return Fail("mission board interaction point is outside the bar");
        if (Vector3.Distance(board, layout.DoorPosition) < 2.0f)
            return Fail("mission board is blocking the bar entrance");
        return Pass();
    }

    private static (bool, string) Pass() => (true, string.Empty);
    private static (bool, string) Fail(string reason) => (false, reason);
}
