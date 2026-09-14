using System;
using System.IO;
using System.Linq;
using FlagFootballStudio.Domain;

namespace FlagFootballStudio.Persistence;

public sealed class ProjectAudioAssetStore
{
    private readonly string _projectDirectory;

    public ProjectAudioAssetStore(string projectFilePath)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath)) throw new ArgumentException("A project file path is required.", nameof(projectFilePath));
        _projectDirectory = Path.GetFullPath(Path.GetDirectoryName(projectFilePath) ?? ".");
    }

    public VoiceAudioReference Import(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("The selected voice audio file does not exist.", sourcePath);
        var format = FormatForExtension(Path.GetExtension(sourcePath));
        var safeName = string.Concat(Path.GetFileNameWithoutExtension(sourcePath).Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "voice";
        var relative = $"audio/{Guid.NewGuid():N}_{safeName}{Path.GetExtension(sourcePath).ToLowerInvariant()}";
        var destination = Resolve(new VoiceAudioReference(relative, format));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(sourcePath, destination, false);
        return new VoiceAudioReference(relative, format);
    }

    public VoiceAudioReference CreateGeneratedWaveReference(Guid lineId, Guid requestId)
    {
        if (lineId == Guid.Empty || requestId == Guid.Empty)
            throw new ArgumentException("Line and request IDs are required for generated audio.");
        var reference = new VoiceAudioReference($"audio/generated/{lineId:N}_{requestId:N}.wav", VoiceAudioFormat.Wav);
        var path = Resolve(reference);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path)) throw new IOException("The collision-safe generated audio destination already exists.");
        return reference;
    }

    public VoiceAudioReference CreateAmbientGeneratedWaveReference(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey) || cacheKey.Any(character => !char.IsAsciiHexDigit(character)))
            throw new ArgumentException("A hexadecimal ambient speech cache key is required.", nameof(cacheKey));
        var reference = new VoiceAudioReference(
            $"audio/generated/ambient/{cacheKey.ToLowerInvariant()}.wav", VoiceAudioFormat.Wav);
        Directory.CreateDirectory(Path.GetDirectoryName(Resolve(reference))!);
        return reference;
    }

    public string AmbientMetadataPath(VoiceAudioReference reference)
    {
        if (reference.Format != VoiceAudioFormat.Wav ||
            !reference.RelativePath.StartsWith("audio/generated/ambient/", StringComparison.Ordinal))
            throw new ArgumentException("Ambient metadata requires an ambient generated WAV reference.", nameof(reference));
        return Path.ChangeExtension(Resolve(reference), ".json");
    }

    public bool IsValidWave(VoiceAudioReference reference)
    {
        if (reference.Format != VoiceAudioFormat.Wav) return false;
        var path = Resolve(reference);
        if (!File.Exists(path) || new FileInfo(path).Length < 44) return false;
        Span<byte> header = stackalloc byte[12];
        using var stream = File.OpenRead(path);
        return stream.Read(header) == header.Length &&
               header[..4].SequenceEqual("RIFF"u8) &&
               header[8..12].SequenceEqual("WAVE"u8);
    }

    public string Resolve(VoiceAudioReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var combined = Path.GetFullPath(Path.Combine(_projectDirectory, reference.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        var root = _projectDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The audio reference points outside the project directory.");
        return combined;
    }

    public bool Exists(VoiceAudioReference reference) => File.Exists(Resolve(reference));

    public static bool IsSupported(string path)
    {
        try { _ = FormatForExtension(Path.GetExtension(path)); return true; }
        catch (NotSupportedException) { return false; }
    }

    private static VoiceAudioFormat FormatForExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".wav" => VoiceAudioFormat.Wav,
        ".ogg" => VoiceAudioFormat.OggVorbis,
        ".mp3" => VoiceAudioFormat.Mp3,
        _ => throw new NotSupportedException("Voice audio must be WAV, OGG Vorbis, or MP3.")
    };
}
