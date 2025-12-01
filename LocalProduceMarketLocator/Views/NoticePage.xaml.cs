using LocalProduceMarketLocator.ViewModels;

namespace LocalProduceMarketLocator.Views;

public partial class NoticePage : ContentPage
{
    // 改了这里
    private readonly NoticeViewModel _viewModel;

    // 改了这里
    public NoticePage(NoticeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadProfileAsync();
    }
}