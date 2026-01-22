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
            Details = $"S{session.MediaParentIndex} · E{session.MediaIndex} — {session.MediaTitle}",
            State = $"{session.MediaGrandParentTitle}",
            Assets = new Assets
            {
                SmallImageKey = playerState.SmallAssetImageKey,
                LargeImageKey = session.Thumbnail
            },
            Timestamps = playerState.Timestamps
        };
    }
}