using System;
using System.Collections.Generic;

namespace Quartermaster.Server.Events.Subscriptions;

public record EventReminderNotificationPayload(Guid EventId, IReadOnlyList<Guid> UserIds);
