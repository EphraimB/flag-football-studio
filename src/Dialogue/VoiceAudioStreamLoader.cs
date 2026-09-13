using System;
using FlagFootballStudio.Domain;
using FlagFootballStudio.Persistence;
using Godot;

namespace FlagFootballStudio.Presentation;

public static class VoiceAudioStreamLoader
{
    public static AudioStream Load(ProjectAudioAssetStore store, VoiceAudioReference reference)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(reference);
        var path = store.Resolve(reference);
        if (!store.Exists(reference)) throw new System.IO.FileNotFoundException("The assigned voice audio asset is missing.", path);
        AudioStream? stream = reference.Format switch
        {
            VoiceAudioFormat.Wav => (AudioStream?)AudioStreamWav.LoadFromFile(path, new Godot.Collections.Dictionary()),
            VoiceAudioFormat.OggVorbis => (AudioStream?)AudioStreamOggVorbis.LoadFromFile(path),
            VoiceAudioFormat.Mp3 => (AudioStream?)AudioStreamMP3.LoadFromFile(path),
            _ => throw new NotSupportedException($"Unsupported voice audio format {reference.Format}.")
        };
        return stream ?? throw new InvalidOperationException("Godot could not decode the assigned voice audio asset.");
    }

    public static double Duration(ProjectAudioAssetStore store, VoiceAudioReference reference) => Load(store, reference).GetLength();
}
