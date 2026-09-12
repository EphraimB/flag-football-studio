using System;
using System.Linq;

namespace FlagFootballStudio.Domain;

public enum FieldDirection
{
    NegativeY = -1,
    PositiveY = 1
}

public static class PlayDirectionResolver
{
    private const float MinimumSeparation = 0.001f;

    public static FieldDirection Resolve(PlayDefinition play, Team offense, Team defense)
    {
        ArgumentNullException.ThrowIfNull(play);
        ArgumentNullException.ThrowIfNull(offense);
        ArgumentNullException.ThrowIfNull(defense);

        var offenseY = AverageFormationY(play, offense);
        var defenseY = AverageFormationY(play, defense);
        if (offenseY.HasValue && defenseY.HasValue)
        {
            var separation = defenseY.Value - offenseY.Value;
            if (MathF.Abs(separation) >= MinimumSeparation)
                return separation > 0 ? FieldDirection.PositiveY : FieldDirection.NegativeY;
        }

        return FieldDirection.PositiveY;
    }

    public static int Sign(FieldDirection direction) => (int)direction;

    public static PlayPoint Advance(PlayPoint point, FieldDirection direction, float distance = 1) =>
        new(point.X, point.Y + Sign(direction) * distance);

    private static float? AverageFormationY(PlayDefinition play, Team team)
    {
        var positions = team.Roster
            .Where(player => play.StartingPositions.ContainsKey(player.Id))
            .Select(player => play.StartingPositions[player.Id].Y)
            .ToArray();
        return positions.Length == 0 ? null : positions.Average();
    }
}
