using LocalProduceMarketLocator.ViewModels;

namespace LocalProduceMarketLocator.Views;

public partial class RegisterPage : ContentPage
{
    public RegisterPage(RegisterViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}


