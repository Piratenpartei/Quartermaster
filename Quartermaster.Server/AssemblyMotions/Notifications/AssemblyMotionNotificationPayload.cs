using System;
using System.Collections.Generic;

namespace Quartermaster.Server.AssemblyMotions.Notifications;

public record AssemblyMotionNotificationPayload(Guid MotionId, Guid EventChapterId, IReadOnlyList<Guid> MemberIds, bool IncludeCommission);
