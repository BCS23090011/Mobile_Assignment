using Android.App;
using Android.Content.PM;
using Android.OS;

namespace LocalProduceMarketLocator;

// 👇👇👇 重点修改这里 👇👇👇
[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,

    // 1. 设置 App 名字
    Label = "Sustain Market",

    // 2. 明确指定图标 (对应 appicon.svg 生成的资源名)
    Icon = "@mipmap/appicon",
    RoundIcon = "@mipmap/appicon_round", // MAUI 会自动生成圆角版

    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}