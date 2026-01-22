using Microsoft.Extensions.Logging;
using Plex.ServerApi.Clients.Interfaces;
using Plex.ServerApi.PlexModels.Server.Sessions;
using PlexRichPresence.Core;
using PlexRichPresence.ViewModels.Models;
using PlexRichPresence.ViewModels.Services;

namespace PlexRichPresence.PlexActivity;

public class PlexSessionsPollingStrategy : IPlexSessionStrategy
{
    private volatile bool _isDisconnected;
    private PlexSession? _lastYieldedSession;

    private readonly IClock _clock;
    private readonly ILogger<PlexSessionsPollingStrategy> _logger;
    private readonly IPlexServerClient _plexServerClient;
    private readonly PlexSessionMapper _plexSessionMapper;

    public PlexSessionsPollingStrategy(ILogger<PlexSessionsPollingStrategy> logger, IPlexServerClient plexServerClient,
        IClock clock, PlexSessionMapper plexSessionMapper)
    {
        _logger = logger;
        _plexServerClient = plexServerClient;
        _clock = clock;
        _plexSessionMapper = plexSessionMapper;
    }


    public async IAsyncEnumerable<PlexSession> GetSessions(string username, string serverIp, int serverPort,
        string userToken)
    {
        _logger.LogInformation("Listening to sessions via polling for user : {Username}", username);
        var plexServerHost = new Uri($"http://{serverIp}:{serverPort}").ToString();
        
        while (!_isDisconnected)
        {
            SessionContainer? sessions = null;
            bool hasError = false;
            
            try
            {
                sessions = await _plexServerClient.GetSessionsAsync(
                    userToken,
                    plexServerHost
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching sessions from Plex server");
                hasError = true;
            }

            PlexSession? currentSession = null;

            if (!hasError && sessions?.Metadata is not null)
            {
                var currentUserSessions = sessions
                    .Metadata
                    .Where(s => s.User.Title == username)
                    .Select(s => _plexSessionMapper.Map(s, plexServerHost, userToken));

                currentSession = SelectActiveSessionFromUserSessions(currentUserSessions);
            }

            // Default to idle session if no active session found or error occurred
            currentSession ??= new PlexSession();

            // Only yield if the session has changed
            if (!SessionsAreEqual(_lastYieldedSession, currentSession))
            {
                if (currentSession.PlayerState == PlexPlayerState.Idle)
                {
                    _logger.LogInformation("No session : Idling");
                }
                else
                {
                    _logger.LogInformation("Found session {Session}", currentSession.MediaParentTitle);
                }

                _lastYieldedSession = currentSession;
                yield return currentSession;
            }
            
            await _clock.Delay(TimeSpan.FromMilliseconds(1000));
        }
    }

    private static PlexSession? SelectActiveSessionFromUserSessions(IEnumerable<PlexSession> currentUserSessions)
    {
        // Materialize to avoid multiple enumeration
        var sessions = currentUserSessions.ToList();
        
        return sessions.FirstOrDefault(s => s.PlayerState == PlexPlayerState.Playing) ??
               sessions.FirstOrDefault(s => s.PlayerState == PlexPlayerState.Buffering) ??
               sessions.FirstOrDefault(s => s.PlayerState == PlexPlayerState.Paused) ??
               sessions.FirstOrDefault(s => s.PlayerState == PlexPlayerState.Idle);
    }

    private static bool SessionsAreEqual(PlexSession? session1, PlexSession? session2)
    {
        if (session1 is null || session2 is null)
        {
            return false;
        }

        // Compare all relevant fields that would require a Discord presence update
        return session1.MediaTitle == session2.MediaTitle &&
               session1.MediaParentTitle == session2.MediaParentTitle &&
               session1.MediaGrandParentTitle == session2.MediaGrandParentTitle &&
               session1.MediaIndex == session2.MediaIndex &&
               session1.MediaParentIndex == session2.MediaParentIndex &&
               session1.PlayerState == session2.PlayerState &&
               session1.MediaType == session2.MediaType &&
               session1.Thumbnail == session2.Thumbnail &&
               Math.Abs(session1.ViewOffset - session2.ViewOffset) < 5000; // Allow 5 second drift
    }

    public void Disconnect()
    {
        _logger.LogInformation("Disconnected");
        _isDisconnected = true;
    }
}