namespace Quartermaster.Api.Submissions;

public enum SubmissionConfirmStatus {
    Confirmed,
    AlreadyConfirmed,
    Expired,
    NotFound
}

public class SubmissionConfirmResultDTO {
    public SubmissionConfirmStatus Status { get; set; }
}
