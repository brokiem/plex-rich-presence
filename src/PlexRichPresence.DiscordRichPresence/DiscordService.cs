using DiscordRPC;
using Microsoft.Extensions.Logging;
using PlexRichPresence.Core;
using PlexRichPresence.DiscordRichPresence.Rendering;
using PlexRichPresence.ViewModels.Services;

namespace PlexRichPresence.DiscordRichPresence;

public class DiscordService : IDiscordService
{
    private readonly ILogger<DiscordService> logger;
    private DiscordRpcClient? discordRpcClient;
    private readonly PlexSessionRenderingService plexSessionRenderingService;
    private PlexSession? currentSession;
    private CancellationTokenSource stopTokenSource = new();
    private readonly SemaphoreSlim stopSemaphore = new(1, 1);

    public DiscordService(ILogger<DiscordService> logger, PlexSessionRenderingService plexSessionRenderingService)
    {
        this.logger = logger;
        this.plexSessionRenderingService = plexSessionRenderingService;
        this.discordRpcClient = CreateRpcClient();
    }

    private DiscordRpcClient CreateRpcClient()
    {
        var rpcClient = new DiscordRpcClient(applicationID: "698954724019273770");
        rpcClient.OnError += (sender, args) => this.logger.LogError(args.Message);
        rpcClient.Initialize();

        return rpcClient;
    }

    public void SetDiscordPresenceToPlexSession(PlexSession session)
    {
        // Check if this is a meaningful change
        // Compare key properties to detect changes faster
        bool isSignificantChange = currentSession == null ||
                                    session.MediaTitle != currentSession.MediaTitle ||
                                    session.PlayerState != currentSession.PlayerState ||
                                    session.ViewOffset != currentSession.ViewOffset ||
                                    session.MediaType != currentSession.MediaType;
        
        if (!isSignificantChange)
        {
            return;
        }

        try
        {
            stopTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Token source was already disposed, create a new one
        }

        currentSession = session;
        RichPresence richPresence = plexSessionRenderingService.RenderSession(session);
        discordRpcClient ??= CreateRpcClient();
        discordRpcClient.SetPresence(richPresence);
        
        logger.LogDebug("Updated Discord RPC: {Title} - {State}", session.MediaTitle, session.PlayerState);
    }

    public async Task StopRichPresence()
    {
        // Use semaphore to prevent concurrent calls
        if (!await stopSemaphore.WaitAsync(0))
        {
            return; // Already stopping
        }

        try
        {
            await Task.Delay(3_000, stopTokenSource.Token);

            discordRpcClient?.Deinitialize();
            discordRpcClient = null;
            currentSession = null;
        }
        catch (TaskCanceledException)
        {
            // Task was cancelled, this is expected when a new session starts
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping rich presence");
        }
        finally
        {
            stopTokenSource.Dispose();
            stopTokenSource = new CancellationTokenSource();
            stopSemaphore.Release();
        }
    }
}