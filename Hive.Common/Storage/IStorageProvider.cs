namespace Hive.Common.Storage;

public interface IStorageProvider
{
    void Save(string json);
    string? Load();
    bool HasSave();
    void Delete();
}
