using System;
using System.Linq;
using Godot;

namespace FlagFootballStudio.Presentation;

public enum CharacterVisualBackend
{
    ProceduralLegacy,
    ImportedModular
}

public readonly record struct CharacterVisualRequest(
    CharacterVisualBackend Backend,
    string ImportedAssetPath)
{
    public static CharacterVisualRequest Default => new(
        CharacterVisualBackend.ProceduralLegacy,
        ImportedCharacterAssetContract.DefaultAssetPath);

    public static CharacterVisualRequest FromCommandLine()
    {
        var request = Default;
        foreach (var argument in OS.GetCmdlineUserArgs())
        {
            if (argument.Equals("--character-visual=imported", StringComparison.OrdinalIgnoreCase))
                request = request with { Backend = CharacterVisualBackend.ImportedModular };
            else if (argument.Equals("--character-visual=legacy", StringComparison.OrdinalIgnoreCase))
                request = request with { Backend = CharacterVisualBackend.ProceduralLegacy };
            else if (argument.StartsWith("--character-asset=", StringComparison.OrdinalIgnoreCase))
                request = request with { ImportedAssetPath = argument.Split('=', 2).Last() };
        }
        return request;
    }
}

public readonly record struct CharacterVisualStatus(
    CharacterVisualBackend RequestedBackend,
    CharacterVisualBackend ActiveBackend,
    string AssetPath,
    bool UsedFallback,
    string Message);
