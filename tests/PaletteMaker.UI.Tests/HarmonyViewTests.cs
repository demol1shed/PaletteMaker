using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PaletteMaker.Controls;
using PaletteMaker.Core;
using PaletteMaker.ViewModels;
using PaletteMaker.Views;
using Xunit;
using static PaletteMaker.UI.Tests.ShellTestHelpers;

namespace PaletteMaker.UI.Tests;

/// <summary>Renk uyumu ekranı: görünüm modeli, HEX girişi, çark etkileşimi, kartlar ve yerleşim.</summary>
public class HarmonyViewTests
{
    // ---- Görünüm modeli (Avalonia fırçası yarattığı için [AvaloniaFact]) -------------------

    [AvaloniaFact]
    public void Default_state_has_five_cards_without_shares()
    {
        var vm = new HarmonyViewModel();
        Assert.Equal(5, vm.Swatches.Count);
        Assert.Equal(5, vm.Colors.Count);
        Assert.False(vm.HasShares);
        Assert.All(vm.Swatches, s =>
        {
            Assert.Equal("", s.ShareText);
            Assert.DoesNotContain("%", s.AccessibleName);
            Assert.Contains(s.Hex, s.AccessibleName);
            Assert.False(s.OffersHarmonyAction);
        });
        Assert.Equal(vm.BaseHex, vm.Swatches[ColorHarmony.BaseIndex].Hex);
        Assert.Equal(vm.BaseHex, vm.HexInput);
    }

    [AvaloniaTheory]
    [InlineData("#E63946")] [InlineData("#2A9D8F")] [InlineData("#264653")] [InlineData("#F4A261")] [InlineData("#FFFF00")]
    [InlineData("#0000FF")] [InlineData("#808080")] [InlineData("#FFFFFF")] [InlineData("#000000")] [InlineData("#123456")]
    public void Hex_input_sets_the_base_color_exactly(string hex)
    {
        var vm = new HarmonyViewModel { HexInput = hex };
        vm.ApplyHexInput();
        Assert.False(vm.HexInvalid);
        Assert.Equal(hex, vm.BaseHex);
        Assert.Equal(hex, vm.Swatches[ColorHarmony.BaseIndex].Hex);
    }

    [AvaloniaFact]
    public void Invalid_hex_flags_an_error_and_keeps_the_base()
    {
        var vm = new HarmonyViewModel();
        var before = vm.BaseHex;
        vm.HexInput = "#12G456";
        vm.ApplyHexInput();
        Assert.True(vm.HexInvalid);
        Assert.Equal(before, vm.BaseHex);
        vm.HexInput = "#abc"; // kısa biçim geçerli
        vm.ApplyHexInput();
        Assert.False(vm.HexInvalid);
        Assert.Equal("#AABBCC", vm.BaseHex);
    }

    [AvaloniaFact]
    public void Rule_change_keeps_the_base_and_file_name_follows_rule_and_color()
    {
        var vm = new HarmonyViewModel { HexInput = "#E63946" };
        vm.ApplyHexInput();
        foreach (var rule in Enum.GetValues<HarmonyRule>())
        {
            vm.RuleIndex = (int)rule;
            Assert.Equal(5, vm.Swatches.Count);
            Assert.Equal("#E63946", vm.BaseHex);
            Assert.Equal($"{rule.ToString().ToLowerInvariant()}-E63946", vm.BaseFileName);
        }
        Assert.Equal("shades-E63946-palette.gpl", vm.ExportFileName("gpl"));
    }

    [AvaloniaFact]
    public void Dragging_a_marker_rotates_the_whole_harmony()
    {
        var vm = new HarmonyViewModel { Rule = HarmonyRule.Triad };
        var start = vm.Colors;
        // Triad: 0. sıra tabanın +120° yanındadır. Onu 200°'ye sürüklemek tabanı 80°'ye getirir.
        vm.DragMarker(0, 200, 0.5);
        Assert.Equal(80, vm.BaseHue, 6);
        Assert.Equal(0.8, vm.BaseSaturation, 6);              // taban dışı işaretçi doygunluğu değiştirmez
        Assert.NotEqual(start[2].H, vm.Colors[2].H);

        vm.DragMarker(ColorHarmony.BaseIndex, 10, 0.3);       // taban işaretçisi: ton ve doygunluk
        Assert.Equal(10, vm.BaseHue, 6);
        Assert.Equal(0.3, vm.BaseSaturation, 6);
        vm.DragMarker(ColorHarmony.BaseIndex, -20, 5);        // sarma ve sıkıştırma
        Assert.Equal(340, vm.BaseHue, 6);
        Assert.Equal(1, vm.BaseSaturation, 6);
        vm.DragMarker(99, 0, 0);                              // geçersiz sıra yok sayılır
        Assert.Equal(340, vm.BaseHue, 6);
    }

    [AvaloniaFact]
    public void Brightness_changes_every_color_and_exports_have_no_share()
    {
        var vm = new HarmonyViewModel();
        var before = vm.Palette.Select(c => c.Hex).ToList();
        vm.BaseValue = 0.5;
        Assert.NotEqual(before, vm.Palette.Select(c => c.Hex).ToList());
        Assert.DoesNotContain("share", vm.ExportText(ExportKind.Json));
        Assert.StartsWith("GIMP Palette", vm.ExportText(ExportKind.GimpPalette));
        Assert.Equal(5, vm.ExportText(ExportKind.HexList).Split('\n').Length);
        Assert.NotEmpty(vm.RenderCardPng());
    }

    // ---- Görünüm ---------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task Hex_box_applies_on_enter_and_shows_an_error_for_bad_input()
    {
        var (w, v, vm) = await OpenShellAsync(940, 700, loadImage: false, tab: ShellTab.Harmony);
        try
        {
            var box = v.Find<TextBox>("HexBox");
            var error = v.Find<TextBlock>("HexError");
            Assert.False(error.IsEffectivelyVisible);

            vm.Harmony.HexInput = "#2A9D8F";
            box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); Settle();
            Assert.Equal("#2A9D8F", vm.Harmony.BaseHex);
            Assert.Equal("#2A9D8F", vm.Harmony.Swatches[2].Hex);

            vm.Harmony.HexInput = "zzz";
            box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); Settle();
            Assert.True(error.IsEffectivelyVisible);
            Assert.Equal("#2A9D8F", vm.Harmony.BaseHex);
            Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(error));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public async Task Wheel_arrow_keys_move_the_base_marker()
    {
        var (w, v, vm) = await OpenShellAsync(940, 700, loadImage: false, tab: ShellTab.Harmony);
        try
        {
            var wheel = v.Find<ColorWheel>("Wheel");
            Assert.True(wheel.Focusable);
            double h0 = vm.Harmony.BaseHue, s0 = vm.Harmony.BaseSaturation;

            void Press(Key key, KeyModifiers m = KeyModifiers.None) =>
                wheel.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = m });

            Press(Key.Right); Settle();
            Assert.Equal(RybWheel.Normalize(h0 + 5), vm.Harmony.BaseHue, 6);
            Press(Key.Left, KeyModifiers.Shift); Settle();
            Assert.Equal(RybWheel.Normalize(h0 + 5 - 15), vm.Harmony.BaseHue, 6);
            Press(Key.Down); Settle();
            Assert.Equal(s0 - 0.05, vm.Harmony.BaseSaturation, 6);
            for (int i = 0; i < 6; i++) Press(Key.Up); Settle();
            Assert.Equal(1.0, vm.Harmony.BaseSaturation, 6);                 // 1'in üstüne çıkmaz
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(wheel)));
            Assert.Contains(vm.Harmony.BaseHex, AutomationProperties.GetName(wheel));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public async Task Dragging_with_the_mouse_moves_the_base_marker_and_clicking_empty_space_jumps_it()
    {
        var (w, v, vm) = await OpenShellAsync(940, 700, loadImage: false, tab: ShellTab.Harmony);
        try
        {
            var wheel = v.Find<ColorWheel>("Wheel");
            var wb = BoundsIn(wheel, w);
            var center = new Point(wb.X + wb.Width / 2, wb.Y + wb.Height / 2);
            double radius = Math.Min(wb.Width, wb.Height) / 2 - 14;

            // Taban işaretçisi (ton 0 = üst, doygunluk 0,8) → sağa (90°) ve daha dışa sürükle.
            var markerPos = new Point(center.X, center.Y - 0.8 * radius);
            w.MouseDown(markerPos, MouseButton.Left); Settle();
            var target = new Point(center.X + 0.9 * radius, center.Y);
            w.MouseMove(target); Settle();
            Assert.Equal(90, vm.Harmony.BaseHue, 1);
            Assert.Equal(0.9, vm.Harmony.BaseSaturation, 2);
            w.MouseUp(target, MouseButton.Left); Settle();

            // Bırakınca sürükleme biter.
            w.MouseMove(new Point(center.X, center.Y + radius * 0.5)); Settle();
            Assert.Equal(90, vm.Harmony.BaseHue, 1);

            // İşaretçilerden uzak boş bir noktaya basmak tabanı oraya taşır (aşağı = 180°).
            var empty = new Point(center.X, center.Y + 0.6 * radius);
            w.MouseDown(empty, MouseButton.Left); Settle();
            w.MouseUp(empty, MouseButton.Left); Settle();
            Assert.Equal(180, vm.Harmony.BaseHue, 1);
            Assert.Equal(0.6, vm.Harmony.BaseSaturation, 2);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public async Task Cards_copy_hex_on_enter_and_show_no_share_percent()
    {
        var (w, v, vm) = await OpenShellAsync(412, 915, loadImage: false, tab: ShellTab.Harmony);
        try
        {
            var cards = v.HarmonyViewPage.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("swatch")).ToList();
            Assert.Equal(5, cards.Count);
            Assert.All(cards, c =>
            {
                Assert.True(c.Focusable);
                Assert.DoesNotContain("%", AutomationProperties.GetName(c));
            });

            var clipboard = TopLevel.GetTopLevel(v)?.Clipboard;
            if (clipboard == null) { Assert.Skip("Headless oturum pano sağlamıyor"); return; }
            await clipboard.ClearAsync();
            cards[3].Focus(); Settle();
            cards[3].RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = cards[3] }); Settle();
            Assert.Equal(vm.Harmony.Swatches[3].Hex, await clipboard.TryGetTextAsync());
            Assert.False(string.IsNullOrEmpty(vm.Harmony.ToastMessage) && !App.SystemShowsClipboardConfirmation);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public async Task Copy_all_and_share_work_and_the_sheet_exports_the_harmony_palette()
    {
        var (w, v, vm) = await OpenShellAsync(940, 700, loadImage: false, tab: ShellTab.Harmony);
        try
        {
            Assert.True(v.Find<Button>("HarmonyCopyAllButton").IsEnabled);
            v.Find<Button>("HarmonyShareButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Settle();
            Assert.True(vm.Harmony.IsExportOpen);
            Assert.False(vm.Main.IsExportOpen);
            Assert.True(v.ExportSheetView.Find<Grid>("ExportSheetHost").IsEffectivelyVisible);
            var clipboard = TopLevel.GetTopLevel(v)?.Clipboard;
            if (clipboard == null) { Assert.Skip("Headless oturum pano sağlamıyor"); return; }
            var json = v.ExportSheetView.GetVisualDescendants().OfType<Button>().First(b => (b.Tag as string) == "Json");
            json.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Settle();
            var text = await clipboard.TryGetTextAsync();
            Assert.NotNull(text);
            Assert.Contains("\"hex\"", text);
            Assert.DoesNotContain("\"share\"", text);
        }
        finally { w.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(940, 700, false)] [InlineData(360, 740, true)]
    public async Task Layout_is_two_columns_when_wide_and_stacked_when_narrow(int width, int height, bool narrow)
    {
        var (w, v, _) = await OpenShellAsync(width, height, loadImage: false, tab: ShellTab.Harmony);
        try
        {
            var hv = v.HarmonyViewPage;
            Assert.Equal(narrow, hv.IsNarrow);
            var controls = BoundsIn(hv.Find<StackPanel>("ControlsPanel"), v);
            var cards = BoundsIn(hv.Find<StackPanel>("CardsPanel"), v);
            if (narrow)
            {
                Assert.True(cards.Top >= controls.Bottom - 0.5, $"kartlar kontrollerin altında olmalı: {controls} / {cards}");
                Assert.True(cards.Width > width - 40, $"kartlar tam genişlikte olmalı: {cards.Width}");
                // Dokunma hedefleri ≥ 40 px.
                Assert.True(hv.Find<ComboBox>("RuleBox").Bounds.Height >= 40);
                Assert.True(hv.Find<ComboBox>("HarmonyFormatBox").Bounds.Height >= 40);
                Assert.True(hv.Find<TextBox>("HexBox").Bounds.Height >= 40);
                Assert.True(hv.Find<Button>("HarmonyCopyAllButton").Bounds.Height >= 40);
                Assert.True(hv.Find<Button>("HarmonyShareButton").Bounds.Height >= 40);
                Assert.All(hv.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("valueChip")),
                    c => Assert.True(c.Bounds.Height >= 40, $"değer çipi {c.Bounds.Height}"));
            }
            else
            {
                Assert.Equal(controls.Top, cards.Top, 0.5);
                Assert.True(cards.Left >= controls.Right - 0.5, $"kartlar sağ sütunda olmalı: {controls} / {cards}");
            }
            var wheel = BoundsIn(hv.Find<ColorWheel>("Wheel"), v);
            Assert.Equal(wheel.Width, wheel.Height, 0.5);   // çark kare
            Assert.True(wheel.Width <= ColorWheel.MaxSize + 0.5);
        }
        finally { w.Close(); }
    }

    // ---- Erişilebilirlik ---------------------------------------------------------------------

    private static string? SpokenName(Control c) =>
        AutomationProperties.GetName(c) is { Length: > 0 } n ? n
        : c is ContentControl { Content: string s } ? s
        : c is TextBlock t ? t.Text : null;

    [AvaloniaTheory]
    [InlineData("en", ShellTab.Harmony)] [InlineData("tr", ShellTab.Harmony)]
    [InlineData("en", ShellTab.Image)] [InlineData("tr", ShellTab.Settings)]
    public async Task Every_interactive_control_on_every_tab_and_the_bar_has_a_spoken_name(string culture, ShellTab tab)
    {
        var old = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo(culture);
        try
        {
            var (w, v, vm) = await OpenShellAsync(412, 915, tab: tab);
            try
            {
                var interactive = v.GetVisualDescendants().OfType<Control>()
                    .Where(c => c.IsEffectivelyVisible && c.FindAncestorOfType<ComboBox>() == null && c.FindAncestorOfType<Slider>() == null
                                && (c is Button or Slider or ComboBox or TextBox or RadioButton or ColorWheel || c.Classes.Contains("swatch") || c.Classes.Contains("valueChip")))
                    .Where(c => c is not RepeatButton)
                    .ToList();
                Assert.Contains(interactive, c => c is RadioButton);
                Assert.True(interactive.OfType<RadioButton>().Count() == 3);
                if (tab == ShellTab.Harmony) Assert.Contains(interactive, c => c is ColorWheel);
                foreach (var c in interactive)
                    Assert.False(string.IsNullOrWhiteSpace(SpokenName(c)), $"{c.GetType().Name} '{c.Name}' ({culture}/{tab}) için okunacak ad yok");
                Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(v.Find<Border>("NavBar"))));
                if (tab == ShellTab.Harmony)
                    foreach (var slider in v.HarmonyViewPage.GetVisualDescendants().OfType<Slider>())
                        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(slider)));
            }
            finally { w.Close(); }
        }
        finally { System.Globalization.CultureInfo.CurrentUICulture = old; }
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task Nav_label_and_icon_contrast_on_the_selected_pill_meets_aa(bool dark)
    {
        var variant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
        var (w, v, _) = await OpenShellAsync(412, 915, dark);
        try
        {
            Avalonia.Media.Color Brush(string key) =>
                v.TryFindResource(key, variant, out var res) ? ((Avalonia.Media.ISolidColorBrush)res!).Color : throw new InvalidOperationException(key);
            static double Lin(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            static double Lum(Avalonia.Media.Color c) => 0.2126 * Lin(c.R / 255.0) + 0.7152 * Lin(c.G / 255.0) + 0.0722 * Lin(c.B / 255.0);
            static double Contrast(Avalonia.Media.Color a, Avalonia.Media.Color b)
            { double x = Lum(a), y = Lum(b); return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05); }

            var pill = Brush("NavPill");
            var text = Brush("SystemControlForegroundBaseHighBrush");
            var bar = Brush("SheetBackground");
            Assert.True(Contrast(text, pill) >= 4.5, $"seçili hap üstünde yazı/simge {Contrast(text, pill):0.00}");
            Assert.True(Contrast(text, bar) >= 4.5, "çubuk zemininde yazı");
            Assert.True(Contrast(pill, bar) >= 1.2, "hap çubuktan ayırt edilebilmeli");
        }
        finally { w.Close(); }
    }
}
