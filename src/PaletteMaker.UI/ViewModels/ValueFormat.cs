namespace PaletteMaker.ViewModels;

/// <summary>Kartlarda gösterilen ve kopyalanan renk değeri biçimi. Sıra, başlıktaki açılır menü sırasıyla aynıdır.</summary>
public enum ValueFormat
{
    Rgb = 0,
    Cmyk = 1,
    Hsl = 2,
    Oklch = 3,
}

/// <summary>Panoya kopyalanabilen ya da dosyaya yazılabilen palet metni türleri.</summary>
public enum ExportKind
{
    HexList,
    Css,
    Json,
    AndroidColorsXml,
    GimpPalette,
}
