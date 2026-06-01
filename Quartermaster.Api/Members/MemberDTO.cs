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
    public DateOnly? EntryDate { get; set; }
    public DateOnly? ExitDate { get; set; }
    public bool IsPending { get; set; }
    public bool HasVotingRights { get; set; }

    /// <summary>True if <see cref="ExitDate"/> has arrived or passed. Future exit dates leave the member active until the date hits.</summary>
    public bool IsExited => ExitDate.HasValue && ExitDate.Value <= DateOnly.FromDateTime(DateTime.UtcNow);
}
