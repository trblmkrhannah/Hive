using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Hive.Common.Storage;

namespace Hive.Browser.Features.Storage;

[SupportedOSPlatform("browser")]
public partial class BrowserStorageProvider : IStorageProvider
{
    private const string SaveKey = "hive_savegame";

    [JSImport("globalThis.HiveStorage.save")]
    private static partial bool JsSave(string key, string value);

    [JSImport("globalThis.HiveStorage.load")]
    private static partial string? JsLoad(string key);

    [JSImport("globalThis.HiveStorage.exists")]
    private static partial bool JsExists(string key);

    [JSImport("globalThis.HiveStorage.remove")]
    private static partial void JsRemove(string key);

    public void Save(string json)
    {
        try
        {
            JsSave(SaveKey, json);
        }
        catch
        {
            // Silently fail - storage may not be available
        }
    }

    public string? Load()
    {
        try
        {
            return JsLoad(SaveKey);
        }
        catch
        {
            return null;
        }
    }

    public bool HasSave()
    {
        try
        {
            return JsExists(SaveKey);
        }
        catch
        {
            return false;
        }
    }

    public void Delete()
    {
        try
        {
            JsRemove(SaveKey);
        }
        catch
        {
            // Silently fail - storage may not be available
        }
    }
}
