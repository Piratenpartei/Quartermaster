using System;

namespace Quartermaster.Server.AssemblyMotions;

public record AssemblyMotionViewer(Guid? MemberId, bool IsEligible, bool IsCommission);
