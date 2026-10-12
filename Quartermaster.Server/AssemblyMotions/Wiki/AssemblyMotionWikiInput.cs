using System.Collections.Generic;
using Quartermaster.Data.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions.Wiki;

public record AssemblyMotionWikiInput(
    AssemblyMotionPortal Portal,
    AssemblyMotionType Type,
    AssemblyMotion Motion,
    bool HasOpenInquiry,
    IReadOnlyList<string> CompetingDisplayNumbers,
    string PortalUrl,
    string BotUserName);
