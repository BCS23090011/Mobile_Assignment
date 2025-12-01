using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalProduceMarketLocator.Services;
using LocalProduceMarketLocator.Views;

namespace LocalProduceMarketLocator.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private string successMessage = string.Empty;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Please enter a valid email";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter a password";
            return;
        }

        // Basic email validation
        if (!Email.Contains("@") || !Email.Contains("."))
        {
            ErrorMessage = "Please enter a valid email";
            return;
        }

        IsLoading = true;

        try
        {
            var user = await _authService.LoginAsync(Email, Password);
            if (user != null)
            {
                SuccessMessage = "Successful login";
                // Navigate after a short delay to show success message
                await Task.Delay(1000);
                await Shell.Current.GoToAsync($"//{nameof(HomePage)}");
            }
            else
            {
                ErrorMessage = "Invalid email or password";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Login failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

}


