using Android.App;
using Android.Content;
using AndroidX.Core.Content;
using PaletteMaker.Sharing;

namespace PaletteMaker.Droid.Sharing;

/// <summary>
/// PNG'yi Android'in sistem paylaşım menüsüyle gönderir. Dosya uygulamanın önbelleğine yazılır ve
/// <see cref="FileProvider"/> ile salt okunur geçici bir adres olarak verilir; <b>ek izin gerekmez</b>.
/// </summary>
public sealed class AndroidShareService(Activity activity) : IShareService
{
    /// <summary>AndroidManifest*.xml'deki &lt;provider&gt; ile aynı olmalı.</summary>
    public const string Authority = "io.github.demol1shed.palettemaker.fileprovider";

    public Task<bool> SharePngAsync(byte[] png, string fileName, string title)
    {
        var dir = new Java.IO.File(activity.CacheDir, "shared");
        dir.Mkdirs();
        DeleteStaleFiles(dir);

        var safeName = System.IO.Path.GetFileName(fileName);
        var file = new Java.IO.File(dir, string.IsNullOrWhiteSpace(safeName) ? "palette.png" : safeName);
        System.IO.File.WriteAllBytes(file.AbsolutePath, png);

        var uri = FileProvider.GetUriForFile(activity, Authority, file);
        var send = new Intent(Intent.ActionSend);
        send.SetType("image/png");
        send.PutExtra(Intent.ExtraStream, uri);
        send.ClipData = ClipData.NewRawUri("palette", uri); // izin vermenin güvenilir yolu: ClipData + bayrak
        send.AddFlags(ActivityFlags.GrantReadUriPermission);
        activity.StartActivity(Intent.CreateChooser(send, title));
        return Task.FromResult(true);
    }

    /// <summary>Önbellek şişmesin: bir günden eski paylaşım dosyalarını siler.</summary>
    private static void DeleteStaleFiles(Java.IO.File dir)
    {
        long cutoff = Java.Lang.JavaSystem.CurrentTimeMillis() - 24L * 60 * 60 * 1000;
        foreach (var f in dir.ListFiles() ?? [])
            if (f.LastModified() < cutoff) f.Delete();
    }
}
