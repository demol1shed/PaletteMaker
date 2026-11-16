using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace PaletteMaker.Droid;

/// <summary>Avalonia'yı süreç başlarken (herhangi bir etkinlikten önce) kurar.</summary>
[Application]
public class AndroidApp : AvaloniaAndroidApplication<App>
{
    protected AndroidApp(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).WithInterFont();
}
