using System.Collections.Generic;

namespace Quartermaster.Server.AssemblyMotions.OpenSlides;

public record OpenSlidesSessionTokens(string AccessToken, string? RefreshId);

/// <summary>A poll option: free text for topic polls, or a content object such as <c>motion/12</c> for motion polls.</summary>
public record OpenSlidesPollOption(string? Text, string? ContentObjectId);

public record OpenSlidesPollCreate(int MeetingId, string Title, string ContentObjectId, string PollMethod, IReadOnlyList<OpenSlidesPollOption> Options,
    int EntitledGroupId, int MinVotes, int MaxVotes, int MaxVotesPerOption, bool GlobalYes, bool GlobalNo, bool GlobalAbstain, string OnehundredPercentBase);
