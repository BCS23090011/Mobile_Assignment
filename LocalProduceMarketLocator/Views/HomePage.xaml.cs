using LocalProduceMarketLocator.ViewModels;

namespace LocalProduceMarketLocator.Views;

public partial class HomePage : ContentPage
{
    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        
        // Load user data when page appears
        Loaded += async (s, e) => await viewModel.LoadUserCommand.ExecuteAsync(null);
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is HomeViewModel vm)
        {
            // 每次页面出现（包括从 Map 或 Notice 页返回时），都查一下有没有未读消息
            await vm.CheckUnreadNotificationsAsync();

            await vm.LoadUserCommand.ExecuteAsync(null);
        }
    }
}

