using System;

namespace Quartermaster.Api.Submissions;

/// <summary>
/// Returned by the three public submit endpoints. When <see cref="RequiresConfirmation"/> is
/// <c>true</c> (the anonymous spam-barrier path), the submission is held until the email at
/// <see cref="Email"/> is confirmed and the live entity is created only then. When <c>false</c>
/// (authenticated caller — already trusted, no confirm step), the entity has been created
/// directly and its id is returned in <see cref="CreatedEntityId"/>.
/// </summary>
public class SubmissionAcceptedResponse {
    public string Email { get; set; } = "";
    public bool RequiresConfirmation { get; set; } = true;
    public Guid? CreatedEntityId { get; set; }
}
