using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using PaletteMaker.Core;
using PaletteMaker.Settings;
using PaletteMaker.ViewModels;
using PaletteMaker.Views;
using Xunit;
using static PaletteMaker.UI.Tests.ShellTestHelpers;

namespace PaletteMaker.UI.Tests;

/// <summary>Ayarlar ekranı: kalıcılık, varsayılanlar, çıktı tercihleri ve yerleşim.</summary>
public class SettingsViewTests
{
    private static string TempPath() => Path.Combine(Path.GetTempPath(), "pm-ui-settings-" + Guid.NewGuid().ToString("N"), "settings.json");

    [AvaloniaFact]
    public void Changes_are_saved_immediately_and_reloaded()
    {
        var path = TempPath();
        try
        {
            var vm = new SettingsViewModel(new SettingsService(path));
            vm.DefaultMaxColors = 20;
            vm.DefaultFormatIndex = 2;
            vm.HexLowercase = true;
            vm.CardShowsShare = false;

            var back = new SettingsService(path).Current;
            Assert.Equal(20, back.DefaultMaxColors);
            Assert.Equal(2, back.DefaultValueFormat);
            Assert.True(back.HexLowercase);
            Assert.False(back.CardShowsShare);
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [AvaloniaFact]
    public void Main_view_model_starts_from_saved_defaults()
    {
        var vm = new MainViewModel(new AppSettings { DefaultMaxColors = 24, DefaultMergeStrength = 0.1, DefaultValueFormat = 3 });
        Assert.Equal(24, vm.MaxColors);
        Assert.Equal(0.1, vm.MergeStrength);
        Assert.Equal(ValueFormat.Oklch, vm.ValueFormat);
    }

    [AvaloniaFact]
    public void Hex_style_reaches_cards_copy_text_and_both_screens()
    {
        var settings = new SettingsViewModel(new SettingsService());
        var shell = new ShellViewModel(new MainViewModel(), settings: settings);
        var before = shell.Harmony.Swatches[0].Hex;
        Assert.StartsWith("#", before);

        settings.HexLowercase = true;
        settings.HexWithHash = false;

        var after = shell.Harmony.Swatches[0].Hex;
        Assert.Equal(before.TrimStart('#').ToLowerInvariant(), after);
        Assert.StartsWith(after, shell.Harmony.AllHexText());
        Assert.Contains(after, shell.Harmony.Swatches[0].AccessibleName);
        Assert.Equal(settings.Settings.HexStyle, shell.Main.HexStyle);
    }

    [AvaloniaFact]
    public void Reset_restores_defaults_and_persists()
    {
        var path = TempPath();
        try
        {
            var vm = new SettingsViewModel(new SettingsService(path)) { DefaultMaxColors = 33, HexLowercase = true };
            vm.ResetToDefaultsCommand.Execute(null);
            Assert.Equal(PaletteOptions.DefaultMaxColors, vm.DefaultMaxColors);
            Assert.False(vm.HexLowercase);
            Assert.Equal(PaletteOptions.DefaultMaxColors, new SettingsService(path).Current.DefaultMaxColors);
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [AvaloniaFact]
    public void Language_change_asks_for_a_restart()
    {
        var vm = new SettingsViewModel(new SettingsService());
        Assert.False(vm.RestartNeeded);
        vm.LanguageIndex = (int)LanguageChoice.Turkish;
        Assert.True(vm.RestartNeeded);
    }

    [AvaloniaTheory]
    [InlineData(390, 800, "telefon")]
    [InlineData(1000, 800, "masaustu")]
    public async Task Settings_page_fits_and_touch_targets_are_large_enough(int w, int h, string name)
    {
        var (window, view, _) = await OpenShellAsync(w, h, loadImage: false, tab: ShellTab.Settings);
        try
        {
            Capture(window, $"ayarlar-{name}");
            var page = VisualFind.Find<StackPanel>(view, "Page")!;
            Assert.True(page.Bounds.Width <= w, "yatay taşma yok");
            foreach (var id in new[] { "ThemeBox", "LanguageBox", "DefaultFormatBox", "ResetButton" })
            {
                var c = VisualFind.Find<Control>(view, id)!;
                Assert.True(c.Bounds.Height >= 40, $"{id} yüksekliği {c.Bounds.Height}");
            }
        }
        finally { window.Close(); }
    }
}
