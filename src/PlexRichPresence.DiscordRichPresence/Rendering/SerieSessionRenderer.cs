using DiscordRPC;
using PlexRichPresence.Core;
using PlexRichPresence.ViewModels.Services;

namespace PlexRichPresence.DiscordRichPresence.Rendering;

public class SerieSessionRenderer(IClock clock) : GenericSessionRenderer(clock)
{
    public override RichPresence RenderSession(PlexSession session)
    {
        DiscordPlayerState playerState = RenderPlayerState(session);
        return new RichPresence
        {
            Type = ActivityType.Watching,
            StatusDisplay = StatusDisplayType.State,
            Details = $"S{FormatNumber(session.MediaParentIndex)}E{FormatNumber(session.MediaIndex)} {session.MediaTitle}",
            State = $"{session.MediaGrandParentTitle}",
            Assets = new Assets
            {
                SmallImageKey = playerState.SmallAssetImageKey,
                LargeImageKey = session.Thumbnail
            },
            Timestamps = playerState.Timestamps
        };
    }

    private static string FormatNumber(uint number)
    {
        string result = string.Empty;
        if (number < 10)
        {
            result += 0;
        }
        return result + number;
    }
}