namespace Quartermaster.Api.SupportSignatures;

public enum SignatureRejectionReason {
    NoVotingRight = 0,
    NotResident = 1,
    IllegibleOrIncomplete = 2,
    SignedTwice = 3,
    Other = 4
}
