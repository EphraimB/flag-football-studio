using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlagFootballStudio.Tts;

public sealed record PiperVoiceModelInfo(
    string Name,
    string ModelPath,
    string ConfigurationPath,
    bool ModelAvailable,
    bool ConfigurationAvailable)
{
    public bool IsCompatible => ModelAvailable && ConfigurationAvailable;
    public string AvailabilityText =>
        $"{(IsCompatible ? "Ready" : "Unavailable")}: model {(ModelAvailable ? "available" : "missing")}; " +
        $"matching .onnx.json {(ConfigurationAvailable ? "available" : "missing")}";
}

/// <summary>Read-only discovery for explicitly installed Piper models. It never downloads or modifies files.</summary>
public sealed class PiperVoiceCatalog
{
    public PiperVoiceCatalog(string modelsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelsDirectory);
        ModelsDirectory = Path.GetFullPath(modelsDirectory);
    }

    public string ModelsDirectory { get; }

    public IReadOnlyList<PiperVoiceModelInfo> Discover()
    {
        if (!Directory.Exists(ModelsDirectory)) return [];

        var modelPaths = Directory.EnumerateFiles(ModelsDirectory, "*.onnx", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var configModelPaths = Directory.EnumerateFiles(ModelsDirectory, "*.onnx.json", SearchOption.AllDirectories)
            .Select(path => Path.GetFullPath(path[..^5]))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return modelPaths.Concat(configModelPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new PiperVoiceModelInfo(
                Path.GetFileNameWithoutExtension(path),
                path,
                path + ".json",
                File.Exists(path),
                File.Exists(path + ".json")))
            .ToArray();
    }

    public PiperVoiceModelInfo Inspect(string? modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            return new PiperVoiceModelInfo("No model selected", string.Empty, string.Empty, false, false);
        var fullPath = Path.GetFullPath(modelPath);
        return new PiperVoiceModelInfo(Path.GetFileNameWithoutExtension(fullPath), fullPath,
            fullPath + ".json", File.Exists(fullPath), File.Exists(fullPath + ".json"));
    }
}
