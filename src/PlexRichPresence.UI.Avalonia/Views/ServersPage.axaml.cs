using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PlexRichPresence.ViewModels;

namespace PlexRichPresence.UI.Avalonia.Views;

public partial class ServersPage : UserControl
{
    private static readonly HttpClient SharedHttpClient = new();

    public ServersPage()
    {
        InitializeComponent();
        var serversPageViewModel = this.CreateInstance<ServersPageViewModel>();
        DataContext = serversPageViewModel;
        
        Dispatcher.UIThread.Post(async () =>
        {
            await serversPageViewModel.GetDataCommand.ExecuteAsync(null);
            await LoadProfileImageAsync(serversPageViewModel.ThumbnailUrl);
        });

        // Also subscribe to changes in case thumbnail URL changes after initial load
        serversPageViewModel.PropertyChanged += async (sender, args) =>
        {
            if (args.PropertyName == nameof(ServersPageViewModel.ThumbnailUrl))
            {
                await LoadProfileImageAsync(serversPageViewModel.ThumbnailUrl);
            }
        };
    }

    private async Task LoadProfileImageAsync(string? thumbnailUrl)
    {
        if (string.IsNullOrEmpty(thumbnailUrl))
            return;

        try
        {
            // Load image off UI thread
            var bitmap = await Task.Run(async () =>
            {
                try
                {
                    using var response = await SharedHttpClient.GetAsync(
                        new Uri(thumbnailUrl), 
                        HttpCompletionOption.ResponseHeadersRead);
                    
                    response.EnsureSuccessStatusCode();
                    
                    await using var stream = await response.Content.ReadAsStreamAsync();
                    
                    // Copy to memory stream (Bitmap needs seekable stream)
                    using var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;
                    
                    return new Bitmap(memoryStream);
                }
                catch (Exception)
                {
                    return null;
                }
            });

            if (bitmap != null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var imageControl = this.FindControl<Image>("profilePicture");
                    if (imageControl != null)
                        imageControl.Source = bitmap;
                });
            }
        }
        catch (Exception)
        {
            // Silently fail - profile picture is not critical
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
