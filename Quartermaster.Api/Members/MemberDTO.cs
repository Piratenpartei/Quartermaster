using System;

namespace Quartermaster.Api.Members;

public class MemberDTO {
    public Guid Id { get; set; }
    public int MemberNumber { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? PostCode { get; set; }
    public string? City { get; set; }
    public Guid? ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public DateTime? EntryDate { get; set; }
    public DateTime? ExitDate { get; set; }
    public bool IsPending { get; set; }
    public bool HasVotingRights { get; set; }
}
