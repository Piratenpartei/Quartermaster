using System.Collections.Generic;
using Quartermaster.Server.Forwarding.Redmine;

namespace Quartermaster.Server.Forwarding;

public record RenderedTicket(string Subject, string Description, IReadOnlyList<RedmineCustomFieldValue> CustomFields);
