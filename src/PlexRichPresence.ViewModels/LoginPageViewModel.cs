using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plex.ServerApi.Clients.Interfaces;
using Plex.ServerApi.PlexModels.Account;
using Plex.ServerApi.PlexModels.OAuth;
using PlexRichPresence.ViewModels.Services;

namespace PlexRichPresence.ViewModels;

[INotifyPropertyChanged]
public partial class LoginPageViewModel
{
    private readonly IPlexAccountClient plexClient;
    private readonly INavigationService navigationService;
    private readonly IStorageService storageService;
    private readonly IBrowserService browserService;
    private readonly IClock clock;

    public LoginPageViewModel(IPlexAccountClient plexClient, INavigationService navigationService,
        IStorageService storageService, IBrowserService browserService, IClock clock)
    {
        this.plexClient = plexClient;
        this.navigationService = navigationService;
        this.storageService = storageService;
        this.browserService = browserService;
        this.clock = clock;
    }

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(LoginWithCredentialsCommand))]
    private string login = string.Empty;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(LoginWithCredentialsCommand))]
    private string password = string.Empty;

    [RelayCommand(AllowConcurrentExecutions = false, CanExecute = "CanLoginWithCredentials")]
    private async Task LoginWithCredentials()
    {
        PlexAccount account = await this.plexClient.GetPlexAccountAsync(this.Login, this.Password);
        await StoreTokenAndNavigateToServerPage(account.AuthToken, account.Username);
    }

    private async Task StoreTokenAndNavigateToServerPage(string token, string userName)
    {
        await storageService.PutAsync("plexUserName", userName);
        await storageService.PutAsync("plex_token", token);
        await navigationService.NavigateToAsync("servers");
    }

    public bool CanLoginWithCredentials => !(string.IsNullOrEmpty(this.Login) || string.IsNullOrEmpty(this.password));

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoginWithBrowser()
    {
        OAuthPin oauthUrl = await OpenBrowserWitnPin();
        OAuthPin plexPin = await WaitForBrowserLogin(oauthUrl);

        PlexAccount account = await this.plexClient.GetPlexAccountAsync(plexPin.AuthToken);
        await StoreTokenAndNavigateToServerPage(plexPin.AuthToken, account.Username);
    }

    private async Task<OAuthPin> WaitForBrowserLogin(OAuthPin oauthUrl)
    {
        const int maxAttempts = 150; // 5 minutes with 2-second delays
        int attempts = 0;
        OAuthPin plexPin;
        
        while (attempts < maxAttempts)
        {
            plexPin = await this.plexClient.GetAuthTokenFromOAuthPinAsync(oauthUrl.Id.ToString());
            if (string.IsNullOrEmpty(plexPin.AuthToken))
            {
                await this.clock.Delay(TimeSpan.FromSeconds(2));
                attempts++;
            }
            else
            {
                return plexPin;
            }
        }

        throw new TimeoutException("Browser login timeout after 5 minutes");
    }

    private async Task<OAuthPin> OpenBrowserWitnPin()
    {
        OAuthPin? oauthUrl = await this.plexClient.CreateOAuthPinAsync("");
        await this.browserService.OpenAsync(oauthUrl.Url);
        return oauthUrl;
    }
}