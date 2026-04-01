using System;

namespace Quartermaster.Server.Email;

public record EmailMessage(
    Guid EmailLogId,
    string To,
    string Subject,
    string HtmlBody
);
