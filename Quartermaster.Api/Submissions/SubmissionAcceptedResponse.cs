namespace Quartermaster.Api.Submissions;

/// <summary>Returned by public submit endpoints — the submission is held until the email at <see cref="Email"/> is confirmed.</summary>
public class SubmissionAcceptedResponse {
    public string Email { get; set; } = "";
}
