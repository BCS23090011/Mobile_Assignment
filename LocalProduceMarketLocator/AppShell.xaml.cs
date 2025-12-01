using LocalProduceMarketLocator.Views;

namespace LocalProduceMarketLocator;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // --- 注册那些不在侧边栏显示，但需要跳转的页面 ---

        // 注册注册页
        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));

        Routing.RegisterRoute(nameof(Views.LoginPage), typeof(Views.LoginPage));

        // 注册通知页 (关键！因为我们在 XAML 里删掉它了)
        Routing.RegisterRoute(nameof(NoticePage), typeof(NoticePage));
    }
}