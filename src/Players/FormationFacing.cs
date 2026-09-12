using System;
using System.Collections.Generic;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public static class FormationFacing
{
    public static FieldDirection Apply(
        PlayDefinition play,
        Team offense,
        Team defense,
        IReadOnlyDictionary<Guid, Node3D> pawns)
    {
        var direction = PlayDirectionResolver.Resolve(play, offense, defense);
        var offenseDirection = ToWorldDirection(direction);
        var defenseDirection = -offenseDirection;

        foreach (var player in offense.Roster)
        {
            if (pawns.TryGetValue(player.Id, out var node) && node is PlayerPawn pawn)
                pawn.SetFormationFacingDirection(offenseDirection);
        }

        foreach (var player in defense.Roster)
        {
            if (pawns.TryGetValue(player.Id, out var node) && node is PlayerPawn pawn)
                pawn.SetFormationFacingDirection(defenseDirection);
        }

        return direction;
    }

    public static Vector3 ToWorldDirection(FieldDirection direction) =>
        new(0, 0, PlayDirectionResolver.Sign(direction));
}
