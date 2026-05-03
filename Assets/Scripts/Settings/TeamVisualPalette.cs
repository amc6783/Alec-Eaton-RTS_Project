using System;
using UnityEngine;

[Serializable]
public struct TeamColorEntry
{
    public int teamId;
    public Color color;
}

[CreateAssetMenu(fileName = "TeamVisualPalette", menuName = "RTS/Team Visual Palette")]
public class TeamVisualPalette : ScriptableObject
{
    [Header("Explicit Team Colors")]
    [SerializeField] private TeamColorEntry[] explicitTeamColors =
    {
        new TeamColorEntry { teamId = 0, color = new Color(0.18f, 0.52f, 0.97f) },
        new TeamColorEntry { teamId = 1, color = new Color(0.95f, 0.25f, 0.23f) }
    };

    [Header("Fallback Palette")]
    [SerializeField] private Color[] fallbackCycle =
    {
        new Color(0.18f, 0.52f, 0.97f), // blue
        new Color(0.95f, 0.25f, 0.23f), // red
        new Color(0.16f, 0.75f, 0.29f), // green
        new Color(0.99f, 0.74f, 0.18f), // yellow
        new Color(0.65f, 0.31f, 0.95f), // purple
        new Color(0.10f, 0.83f, 0.88f), // cyan
        new Color(1.00f, 0.53f, 0.16f), // orange
        new Color(0.90f, 0.18f, 0.63f)  // magenta
    };

    [SerializeField] private Color fallbackColor = Color.white;

    public Color GetColorForTeam(int teamId)
    {
        if (TryGetExplicitColor(teamId, out Color explicitColor))
        {
            return explicitColor;
        }

        if (fallbackCycle != null && fallbackCycle.Length > 0)
        {
            int paletteIndex = Mathf.Abs(teamId) % fallbackCycle.Length;
            return fallbackCycle[paletteIndex];
        }

        return fallbackColor;
    }

    private bool TryGetExplicitColor(int teamId, out Color color)
    {
        if (explicitTeamColors != null)
        {
            for (int i = 0; i < explicitTeamColors.Length; i++)
            {
                TeamColorEntry entry = explicitTeamColors[i];
                if (entry.teamId == teamId)
                {
                    color = entry.color;
                    return true;
                }
            }
        }

        color = default;
        return false;
    }
}

