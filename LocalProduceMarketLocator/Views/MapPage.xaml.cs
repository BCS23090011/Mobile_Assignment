using LocalProduceMarketLocator.ViewModels;
using Microsoft.Maui.Controls.Maps;

namespace LocalProduceMarketLocator.Views;

public partial class MapPage : ContentPage
{
    private readonly MapViewModel _viewModel;

    public MapPage(MapViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Map = MarketMap; // change MarketMap.Map = _viewModel.Map; to _viewModel.map=MarketMap
        await _viewModel.InitializeAsync();
    }

    private void OnMapClicked(object? sender, MapClickedEventArgs e)
    {
        // Handle map click if needed
    }
}


