namespace Quartermaster.Api.Users;

/// <summary>How a user appears where Quartermaster shows them informally; formal places always use first and last name.</summary>
public enum DisplayNameMode {
    RealName = 0,
    Username = 1
}
