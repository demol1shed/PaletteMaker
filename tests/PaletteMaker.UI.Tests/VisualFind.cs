using Avalonia.Controls;
using Avalonia;
using Avalonia.VisualTree;

namespace PaletteMaker.UI.Tests;

internal static class VisualFind
{
    /// <summary>
    /// Ada göre denetim bulur. İç içe UserControl'ler (MainView, HarmonyView, ExportSheet) ayrı ad kapsamı olduğu için
    /// <c>FindControl</c> kabuktan onlara inmez; bu yardımcı görsel ağacı tamamen tarar (gizli sekmeler dahil).
    /// </summary>
    public static T Find<T>(this Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name)
        ?? throw new InvalidOperationException($"'{name}' adlı {typeof(T).Name} bulunamadı");
}
