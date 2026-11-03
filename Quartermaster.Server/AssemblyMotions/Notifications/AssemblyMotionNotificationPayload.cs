using System;
using System.Collections.Generic;

namespace Quartermaster.Server.AssemblyMotions.Notifications;

/// <param name="SubscriberEventId">When set, the subscribers of this event who can see it are recipients too.</param>
public record AssemblyMotionNotificationPayload(Guid MotionId, Guid EventChapterId, IReadOnlyList<Guid> MemberIds, bool IncludeCommission, Guid? SubscriberEventId = null);
