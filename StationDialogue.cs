using System;

namespace Roguelancer;

/// <summary>
/// Small, non-branching station conversation payload. It deliberately contains
/// no mission, choice, or persistence state so social NPCs can share one
/// interaction path without becoming an RPG dialogue system.
/// </summary>
public sealed record StationDialogue
{
    public string Speaker { get; }
    public string Text { get; }
    public float Duration { get; }

    public StationDialogue(string speaker, string text, float duration = 3.5f)
    {
        Speaker = speaker ?? string.Empty;
        Text = text ?? string.Empty;
        Duration = MathF.Max(0.1f, duration);
    }
}

public enum StationNpcInteractionRole
{
    Dialogue,
}
