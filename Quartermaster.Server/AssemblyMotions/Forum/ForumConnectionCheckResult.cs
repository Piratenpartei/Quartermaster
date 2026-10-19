using System.Collections.Generic;

namespace Quartermaster.Server.AssemblyMotions.Forum;

/// <param name="Configured">False when the Discourse options are incomplete; no request was made then.</param>
/// <param name="Message">The confirmation when <paramref name="Ok"/>, otherwise a German reason that contains no secret and no response body.</param>
/// <param name="Warnings">Problems that do not block mirroring, such as a bot account without moderator rights.</param>
public record ForumConnectionCheckResult(bool Ok, bool Configured, string Message, IReadOnlyList<string> Warnings);
