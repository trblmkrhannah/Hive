using Hive.Common.Storage;

namespace Hive.Desktop.Features.Storage;

public sealed class FileStorageProvider : IStorageProvider
{
    private readonly string _directory;
    private readonly string _filePath;

    public FileStorageProvider(string directory)
    {
        _directory = directory;
        _filePath = Path.Combine(directory, "savegame.json");
    }

    public void Save(string json)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_filePath, json);
    }

    public string? Load()
    {
        if (!File.Exists(_filePath))
            return null;

        return File.ReadAllText(_filePath);
    }

    public bool HasSave()
    {
        return File.Exists(_filePath);
    }

    public void Delete()
    {
        if (!File.Exists(_filePath))
            return;
        
        File.Delete(_filePath);
    }
}