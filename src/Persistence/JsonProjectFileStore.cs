using System;
using System.IO;
using FlagFootballStudio.Domain;

namespace FlagFootballStudio.Persistence;

public sealed class JsonProjectFileStore
{
    private readonly ProjectJsonSerializer _serializer;

    public JsonProjectFileStore(ProjectJsonSerializer serializer)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public void SaveProject(string path, GameProject project) =>
        Write(path, _serializer.SerializeProject(project));

    public GameProject LoadProject(string path) =>
        _serializer.DeserializeProject(File.ReadAllText(ValidatePath(path)));

    public void SavePlay(string path, PlayDefinition play) =>
        Write(path, _serializer.SerializePlay(play));

    public PlayDefinition LoadPlay(string path) =>
        _serializer.DeserializePlay(File.ReadAllText(ValidatePath(path)));

    private static void Write(string path, string json)
    {
        var validPath = ValidatePath(path);
        var directory = Path.GetDirectoryName(validPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(validPath, json);
    }

    private static string ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A file path is required.", nameof(path));
        return path;
    }
}
