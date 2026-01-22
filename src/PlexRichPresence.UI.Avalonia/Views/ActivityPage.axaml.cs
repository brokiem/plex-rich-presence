using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using PlexRichPresence.ViewModels;

namespace PlexRichPresence.UI.Avalonia.Views;

public partial class ActivityPage : UserControl
{
    private static readonly HttpClient SharedHttpClient = new();
    private CancellationTokenSource? _thumbnailCts;
    private string? _currentThumbnailUrl;

    public ActivityPage()
    {
        InitializeComponent();
        var plexActivityViewModel = this.CreateInstance<PlexActivityPageViewModel>();
        DataContext = plexActivityViewModel;
        
        // Initialize on UI thread but don't block
        Dispatcher.UIThread.Post(async () =>
        {
            await plexActivityViewModel.InitStrategyCommand.ExecuteAsync(null);
            await plexActivityViewModel.StartActivityCommand.ExecuteAsync(null);
        });

        // Subscribe to thumbnail URL changes
        plexActivityViewModel.PropertyChanged += async (sender, args) =>
        {
            if (args.PropertyName == nameof(PlexActivityPageViewModel.ThumbnailUrl))
            {
                await LoadThumbnailAsync(plexActivityViewModel.ThumbnailUrl);
            }
        };
    }

    private async Task LoadThumbnailAsync(string? thumbnailUrl)
    {
        // Cancel any pending thumbnail load
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
        _thumbnailCts = new CancellationTokenSource();
        var cancellationToken = _thumbnailCts.Token;

        // Avoid reloading the same image
        if (thumbnailUrl == _currentThumbnailUrl)
            return;

        _currentThumbnailUrl = thumbnailUrl;

        if (string.IsNullOrEmpty(thumbnailUrl))
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var imageControl = this.FindControl<Image>("thumbnail");
                if (imageControl != null)
                    imageControl.Source = null;
            });
            return;
        }

        try
        {
            // Load image data off the UI thread
            var bitmap = await Task.Run(async () =>
            {
                try
                {
                    using var response = await SharedHttpClient.GetAsync(
                        new Uri(thumbnailUrl), 
                        HttpCompletionOption.ResponseHeadersRead, 
                        cancellationToken);
                    
                    response.EnsureSuccessStatusCode();
                    
                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    
                    // Copy to memory stream (Bitmap needs seekable stream)
                    using var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream, cancellationToken);
                    memoryStream.Position = 0;
                    
                    return new Bitmap(memoryStream);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
                catch (Exception)
                {
                    return null;
                }
            }, cancellationToken);

            if (bitmap != null && !cancellationToken.IsCancellationRequested)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var imageControl = this.FindControl<Image>("thumbnail");
                    if (imageControl != null)
                        imageControl.Source = bitmap;
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Thumbnail load was cancelled, this is expected
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
