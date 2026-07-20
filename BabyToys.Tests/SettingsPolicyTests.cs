using BabyToys.Models;
using BabyToys.Services;

namespace BabyToys.Tests;

[TestClass]
public sealed class SettingsPolicyTests
{
    [TestMethod]
    [DataRow(-1, SettingsPolicy.MinimumDurationMinutes)]
    [DataRow(0, SettingsPolicy.MinimumDurationMinutes)]
    [DataRow(0.5, 0.5)]
    [DataRow(12.25, 12.25)]
    [DataRow(500, SettingsPolicy.MaximumDurationMinutes)]
    public void NormalizeDurationMinutes_ClampsToSupportedRange(double input, double expected)
    {
        Assert.AreEqual(expected, SettingsPolicy.NormalizeDurationMinutes(input));
    }

    [TestMethod]
    public void NormalizeDurationMinutes_UsesSafeDefaultForNonFiniteValues()
    {
        Assert.AreEqual(SettingsPolicy.DefaultDurationMinutes, SettingsPolicy.NormalizeDurationMinutes(double.NaN));
        Assert.AreEqual(SettingsPolicy.DefaultDurationMinutes, SettingsPolicy.NormalizeDurationMinutes(double.PositiveInfinity));
    }

    [TestMethod]
    public void Normalize_CleansPresetsAndRepairsSelection()
    {
        var settings = new AppSettings
        {
            SchemaVersion = 0,
            DurationMinutes = -5,
            SelectedPresetName = " quiet ",
            Presets =
            [
                new ChildModePreset { Name = " Quiet ", DurationMinutes = 999 },
                new ChildModePreset { Name = "quiet", DurationMinutes = 10 },
                new ChildModePreset { Name = "  " }
            ]
        };

        SettingsPolicy.Normalize(settings);

        Assert.AreEqual(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.AreEqual(EntryVisualMode.FadeFromImage, settings.EntryVisualMode);
        Assert.AreEqual(SettingsPolicy.MinimumDurationMinutes, settings.DurationMinutes);
        Assert.HasCount(1, settings.Presets);
        Assert.AreEqual("Quiet", settings.Presets[0].Name);
        Assert.AreEqual(SettingsPolicy.MaximumDurationMinutes, settings.Presets[0].DurationMinutes);
        Assert.AreEqual(EntryVisualMode.FadeFromImage, settings.Presets[0].EntryVisualMode);
        Assert.AreEqual("Quiet", settings.SelectedPresetName);
    }

    [TestMethod]
    public void Normalize_NewSettingsUseImmediateBlack()
    {
        var settings = SettingsPolicy.Normalize(new AppSettings());

        Assert.AreEqual(EntryVisualMode.ImmediateBlack, settings.EntryVisualMode);
    }

    [TestMethod]
    public void Normalize_RejectsNewerSchemaInsteadOfDowngradingIt()
    {
        var settings = new AppSettings { SchemaVersion = AppSettings.CurrentSchemaVersion + 1 };

        Assert.ThrowsExactly<NotSupportedException>(() => SettingsPolicy.Normalize(settings));
        Assert.AreEqual(AppSettings.CurrentSchemaVersion + 1, settings.SchemaVersion);
    }
}
