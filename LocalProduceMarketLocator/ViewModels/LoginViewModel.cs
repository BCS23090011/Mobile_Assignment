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
    private async Task ForgotPasswordAsync()
    {
        // 1. 弹窗让用户输入邮箱 (如果登录框已经填了，可以预设进去)
        string result = await Shell.Current.DisplayPromptAsync("Reset Password",
                                                             "Enter your email address to receive a reset link:",
                                                             initialValue: Email, // 如果用户已经在登录框输了，直接带过来
                                                             accept: "Send Link",
                                                             cancel: "Cancel");

        // 如果用户点了取消或没填内容
        if (string.IsNullOrWhiteSpace(result)) return;

        IsLoading = true;

        // 2. 调用 Service 发送邮件
        bool sent = await _authService.ResetPasswordAsync(result);

        IsLoading = false;

        if (sent)
        {
            await Shell.Current.DisplayAlert("Success", $"A password reset link has been sent to {result}. Please check your email (and spam folder).", "OK");
        }
        else
        {
            await Shell.Current.DisplayAlert("Error", "Failed to send reset email. Please check if the email format is correct or if the account exists.", "OK");
        }
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


