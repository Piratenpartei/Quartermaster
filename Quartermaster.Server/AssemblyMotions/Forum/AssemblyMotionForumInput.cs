using System;
using System.Collections.Generic;
using Quartermaster.Data.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions.Forum;

/// <param name="ResultLabel">Null while the motion has no result.</param>
/// <param name="WikiUrl">Null while the portal does not mirror to the wiki or the motion has no number.</param>
/// <param name="CollectingDeadline">The deadline by which a motion collecting co-submitters needs them.</param>
public record AssemblyMotionForumInput(
    AssemblyMotion Motion,
    AssemblyMotionType Type,
    string EventName,
    IReadOnlyList<string> SubmitterNames,
    string StatusLabel,
    string? ResultLabel,
    string PortalUrl,
    string? WikiUrl,
    int CoSubmitterCount,
    DateTime CollectingDeadline);
