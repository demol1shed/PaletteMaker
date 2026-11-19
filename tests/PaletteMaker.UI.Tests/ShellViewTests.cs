using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PaletteMaker.ViewModels;
using PaletteMaker.Views;
using Xunit;
using static PaletteMaker.UI.Tests.ShellTestHelpers;

namespace PaletteMaker.UI.Tests;

/// <summary>Alt gezinme çubuğu, sekmeler, Geri/Esc/Ctrl+1-3, bildirim ve görselden uyuma köprü.</summary>
public class ShellViewTests
{
    private static void Key(ShellView v, Key key, KeyModifiers mods = KeyModifiers.None)
    {
        v.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = mods });
        Settle();
    }

    [AvaloniaFact]
    public async Task Bar_has_three_items_in_order_and_opens_on_the_image_tab()
    {
        var (w, v, vm) = await OpenShellAsync(412, 915);
        try
        {
            Assert.Equal(ShellTab.Image, vm.Tab);
            var s = BoundsIn(v.Find<RadioButton>("NavSettings"), v);
            var i = BoundsIn(v.Find<RadioButton>("NavImage"), v);
            var h = BoundsIn(v.Find<RadioButton>("NavHarmony"), v);
            Assert.True(s.Right <= i.Left + 0.5 && i.Right <= h.Left + 0.5, $"sıra: {s} {i} {h}");
            Assert.True(v.Find<RadioButton>("NavImage").IsChecked);
            Assert.False(v.Find<RadioButton>("NavSettings").IsChecked);
            Assert.True(s.Top > 700 && s.Bottom <= 915.5, "çubuk altta");
            Assert.All(new[] { s, i, h }, b => Assert.True(b.Height >= 48 && b.Width >= 48, $"dokunma hedefi {b}"));

            Assert.True(v.ImageView.IsEffectivelyVisible);
            Assert.False(v.HarmonyViewPage.IsEffectivelyVisible);
            Assert.False(v.Find<SettingsView>("SettingsPage").IsEffectivelyVisible);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public async Task Clicking_an_item_switches_the_page_and_keeps_the_others_alive()
    {
        var (w, v, vm) = await OpenShellAsync(412, 915);
        try
        {
            var imageView = v.ImageView;
            v.Find<RadioButton>("NavHarmony").IsChecked = true; Settle();
            Assert.Equal(ShellTab.Harmony, vm.Tab);
            Assert.True(v.HarmonyViewPage.IsEffectivelyVisible);
            Assert.False(imageView.IsEffectivelyVisible);
            Assert.True(v.Find<RadioButton>("NavHarmony").IsChecked);
            Assert.False(v.Find<RadioButton>("NavImage").IsChecked);

            v.Find<RadioButton>("NavSettings").IsChecked = true; Settle();
            Assert.Equal(ShellTab.Settings, vm.Tab);
            Assert.True(v.Find<SettingsView>("SettingsPage").IsEffectivelyVisible);

            v.Find<RadioButton>("NavImage").IsChecked = true; Settle();
            Assert.Same(imageView, v.ImageView);
            Assert.True(imageView.IsEffectivelyVisible);
            Assert.NotEmpty(vm.Main.Swatches); // görsel/palet sekme değişince kaybolmaz
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public async Task Ctrl_1_2_3_switch_tabs()
    {
        var (w, v, vm) = await OpenShellAsync(940, 700);
        try
        {
            Key(v, Avalonia.Input.Key.D3, KeyModifiers.Control); Assert.Equal(ShellTab.Harmony, vm.Tab);
            Key(v, Avalonia.Input.Key.D1, KeyModifiers.Control); Assert.Equal(ShellTab.Settings, vm.Tab);
            Key(v, Avalonia.Input.Key.D2, KeyModifiers.Control); Assert.Equal(ShellTab.Image, vm.Tab);
            Key(v, Avalonia.Input.Key.D3); Assert.Equal(ShellTab.Image, vm.Tab); // Ctrl olmadan değişmez
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public async Task Back_closes_the_sheet_first_then_returns_to_the_center_tab_then_is_left_unhandled()
    {
        var (w, v, vm) = await OpenShellAsync(360, 740, tab: ShellTab.Harmony);
        try
        {
            var back = typeof(ShellView).GetMethod("OnBackRequested", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            bool Back() { var a = new RoutedEventArgs(); back.Invoke(v, [null, a]); Settle(); return a.Handled; }

            vm.Harmony.IsExportOpen = true; Settle();
            Assert.True(Back());
            Assert.False(vm.Harmony.IsExportOpen);
            Assert.Equal(ShellTab.Harmony, vm.Tab);   // önce yalnızca alt sayfa kapanır

            Assert.True(Back());
            Assert.Equal(ShellTab.Image, vm.Tab);     // sonra orta sekmeye dönülür

            Assert.False(Back());                      // orta sekmede işlenmez: uygulamadan çıkılabilir
            vm.Tab = ShellTab.Settings;
            Assert.True(Back());
            Assert.Equal(ShellTab.Image, vm.Tab);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public async Task Escape_closes_the_sheet_on_either_screen_and_the_scrim_covers_the_bar()
    {
        var (w, v, vm) = await OpenShellAsync(360, 740);
        try
        {
            foreach (var tab in new[] { ShellTab.Image, ShellTab.Harmony })
            {
                vm.Tab = tab; Settle();
                vm.ActiveScreen!.IsExportOpen = true; Settle();
                var sheetHost = v.ExportSheetView.Find<Grid>("ExportSheetHost");
                Assert.True(sheetHost.IsEffectivelyVisible);
                // Perde gezinme çubuğunu da örter.
                var host = BoundsIn(sheetHost, v);
                Assert.True(host.Bottom >= BoundsIn(v.Find<Border>("NavBar"), v).Bottom - 0.5);
                Key(v, Avalonia.Input.Key.Escape);
                Assert.False(vm.ActiveScreen.IsExportOpen);
            }
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public async Task Changing_tab_closes_an_open_sheet_and_settings_has_no_sheet()
    {
        var (w, v, vm) = await OpenShellAsync(360, 740);
        try
        {
            vm.Main.IsExportOpen = true; Settle();
            vm.Tab = ShellTab.Harmony; Settle();
            Assert.False(vm.Main.IsExportOpen);
            Assert.False(v.ExportSheetView.Find<Grid>("ExportSheetHost").IsEffectivelyVisible);

            vm.Harmony.IsExportOpen = true; Settle();
            vm.Tab = ShellTab.Settings; Settle();
            Assert.False(vm.Harmony.IsExportOpen);
            Assert.Null(vm.ActiveScreen);
            Assert.False(v.ExportSheetView.Find<Grid>("ExportSheetHost").IsEffectivelyVisible);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public async Task Toast_follows_the_active_screen_and_sits_above_the_bar()
    {
        var (w, v, vm) = await OpenShellAsync(360, 740, dark: true);
        try
        {
            var toast = v.Find<Border>("Toast");
            Assert.False(toast.IsEffectivelyVisible);
            vm.Main.ShowToast("#E63946 kopyalandı"); Settle();
            Assert.True(toast.IsEffectivelyVisible);
            var b = BoundsIn(toast, v);
            Assert.True(b.Left >= 0 && b.Right <= 360 && b.Bottom <= BoundsIn(v.Find<Border>("NavBar"), v).Top + 0.5, $"bildirim çubuğun üstünde değil: {b}");
            Capture(w, "kabuk-360x740-koyu-bildirim");

            // Başka bir ekranın bildirimi bu ekranda görünmez.
            vm.Tab = ShellTab.Harmony; Settle();
            Assert.False(toast.IsEffectivelyVisible);
            vm.Harmony.ShowToast("ok"); Settle();
            Assert.True(toast.IsEffectivelyVisible);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public async Task Toast_is_hidden_on_the_settings_tab_where_there_is_no_active_screen()
    {
        var (w, v, vm) = await OpenShellAsync(360, 740);
        try
        {
            var toast = v.Find<Border>("Toast");
            vm.Tab = ShellTab.Settings; Settle();
            Assert.Null(vm.ActiveScreen);
            Assert.False(toast.IsVisible, "ActiveScreen yokken boş bildirim kutusu çizilmemeli");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public async Task Use_in_harmony_switches_the_tab_and_sets_the_base_color()
    {
        var (w, v, vm) = await OpenShellAsync(412, 915);
        try
        {
            var buttons = v.ImageView.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("harmonyAction")).ToList();
            Assert.Equal(vm.Main.Swatches.Count, buttons.Count);
            Assert.All(buttons, b => Assert.False(string.IsNullOrWhiteSpace(Avalonia.Automation.AutomationProperties.GetName(b))));

            var target = vm.Main.Swatches[1];
            buttons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Settle();
            Assert.Equal(ShellTab.Harmony, vm.Tab);
            Assert.Equal(target.Hex, vm.Harmony.BaseHex);
            Assert.Equal(target.Hex, vm.Harmony.Swatches[2].Hex);
            Assert.True(v.HarmonyViewPage.IsEffectivelyVisible);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public async Task Value_format_is_shared_between_both_screens()
    {
        var (w, v, vm) = await OpenShellAsync(412, 915);
        try
        {
            v.Find<ComboBox>("FormatBox").SelectedIndex = 3; Settle();
            Assert.Equal(ValueFormat.Oklch, vm.Harmony.ValueFormat);
            Assert.All(vm.Harmony.Swatches, s => Assert.StartsWith("oklch(", s.ValueText));

            vm.Tab = ShellTab.Harmony; Settle(); // gizli sekmenin şablonu ilk gösterilene kadar uygulanmaz
            v.Find<ComboBox>("HarmonyFormatBox").SelectedIndex = 1; Settle();
            Assert.Equal(ValueFormat.Cmyk, vm.Main.ValueFormat);
            Assert.All(vm.Main.Swatches, s => Assert.StartsWith("cmyk(", s.ValueText));
        }
        finally { w.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(360, 740)] [InlineData(412, 915)] [InlineData(915, 412)] [InlineData(940, 700)] [InlineData(360, 480)]
    public async Task Every_tab_fits_without_horizontal_overflow_and_the_image_screen_keeps_its_layout(int width, int height)
    {
        foreach (bool dark in new[] { false, true })
        {
            var (w, v, vm) = await OpenShellAsync(width, height, dark);
            try
            {
                var pageScroll = v.ImageView.Find<ScrollViewer>("PageScroll");
                Assert.True(pageScroll.Extent.Width <= pageScroll.Viewport.Width + 0.5, "Görsel: yatay taşma");
                // Çubuk yüksekliği düşülünce de 940×700 masaüstü düzeni "short" olmaz.
                if (height >= 700) Assert.False(v.ImageView.Find<Grid>("Root").Classes.Contains("short"));
                Assert.Equal(width < MainView.NarrowBreakpoint, v.ImageView.IsNarrow);

                vm.Tab = ShellTab.Harmony; Settle();
                var hs = v.HarmonyViewPage.Find<ScrollViewer>("HarmonyScroll");
                Assert.True(hs.Extent.Width <= hs.Viewport.Width + 0.5, $"Uyum: yatay taşma {hs.Extent.Width} > {hs.Viewport.Width}");
                Assert.Equal(width < MainView.NarrowBreakpoint, v.HarmonyViewPage.IsNarrow);

                string suffix = $"{width}x{height}-{(dark ? "koyu" : "acik")}";
                Capture(w, $"kabuk-uyum-{suffix}");
                vm.Tab = ShellTab.Settings; Settle();
                Capture(w, $"kabuk-ayarlar-{suffix}");
                vm.Tab = ShellTab.Image; Settle();
                Capture(w, $"kabuk-gorsel-{suffix}");
            }
            finally { w.Close(); }
        }
    }
}
