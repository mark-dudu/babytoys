using BabyToys.Models;
using BabyToys.Services;

namespace BabyToys.Tests;

[TestClass]
public sealed class SettingsServiceTests
{
    private string _directory = null!;
    private string _settingsPath = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"BabyToys.Tests-{Guid.NewGuid():N}");
        _settingsPath = Path.Combine(_directory, "settings.json");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public void SaveAndLoad_RoundTripsNormalizedSettingsAtomically()
    {
        var service = CreateService();
        service.Save(new AppSettings { DurationMinutes = 500 });
        Assert.AreEqual(SettingsPolicy.MaximumDurationMinutes, service.Load().DurationMinutes);

        service.Save(new AppSettings { DurationMinutes = 12.5 });
        var replaced = service.Load();

        Assert.AreEqual(12.5, replaced.DurationMinutes);
        Assert.AreEqual(AppSettings.CurrentSchemaVersion, replaced.SchemaVersion);
        Assert.IsEmpty(Directory.EnumerateFiles(_directory, "*.tmp"));
    }

    [TestMethod]
    public void Load_InvalidJsonBacksUpOriginalAndReturnsDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_settingsPath, "{ invalid json");
        var service = CreateService();

        var loaded = service.Load();

        Assert.AreEqual(SettingsPolicy.DefaultDurationMinutes, loaded.DurationMinutes);
        Assert.IsFalse(File.Exists(_settingsPath));
        Assert.HasCount(1, Directory.EnumerateFiles(_directory, "settings.corrupt-*.json"));
    }

    [TestMethod]
    public void Load_LegacySettingsWithoutSchemaKeepsImageFadeBehavior()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            _settingsPath,
            """
            {
              "ImageSourceMode": 0,
              "DurationMinutes": 8,
              "Presets": [
                {
                  "Name": "旧预设",
                  "DurationMinutes": 8
                }
              ]
            }
            """);
        var service = CreateService();

        var loaded = service.Load();

        Assert.AreEqual(AppSettings.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.AreEqual(EntryVisualMode.FadeFromImage, loaded.EntryVisualMode);
        Assert.AreEqual(EntryVisualMode.FadeFromImage, loaded.Presets[0].EntryVisualMode);
    }

    [TestMethod]
    public void Load_NewerSchemaKeepsOriginalFileAndBlocksSave()
    {
        Directory.CreateDirectory(_directory);
        const string newerSettings = """
            {
              "SchemaVersion": 99,
              "DurationMinutes": 42,
              "FutureOnlySetting": true
            }
            """;
        File.WriteAllText(_settingsPath, newerSettings);
        var service = CreateService();

        var loaded = service.Load();
        service.Save(new AppSettings { DurationMinutes = 10 });

        Assert.IsTrue(service.IsReadOnlyDueToUnsupportedSchema);
        Assert.AreEqual(99, service.UnsupportedSchemaVersion);
        Assert.AreEqual(SettingsPolicy.DefaultDurationMinutes, loaded.DurationMinutes);
        Assert.AreEqual(newerSettings, File.ReadAllText(_settingsPath));
        Assert.IsEmpty(Directory.EnumerateFiles(_directory, "settings.corrupt-*.json"));
    }

    private SettingsService CreateService()
    {
        return new SettingsService(
            _settingsPath,
            () => new DateTimeOffset(2026, 7, 18, 12, 30, 0, TimeSpan.Zero),
            (_, _) => { },
            _ => { });
    }
}
