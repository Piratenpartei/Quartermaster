using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Users;

public class UserSettingsDTO {
    public LoginUserInfo User { get; set; } = new();
    public UserSettingsMemberInfo? Member { get; set; }
    public List<UserSettingsPermissionInfo> GlobalPermissions { get; set; } = [];
    public List<UserSettingsChapterPermissions> ChapterPermissions { get; set; } = [];
}

public class UserSettingsMemberInfo {
    public int MemberNumber { get; set; }
    public string ChapterName { get; set; } = "";
    public DateTime? EntryDate { get; set; }
    public decimal MembershipFee { get; set; }
    public decimal ReducedFee { get; set; }
    public bool HasVotingRights { get; set; }
    public bool IsPending { get; set; }
    public decimal? OpenFeeTotal { get; set; }
}

public class UserSettingsPermissionInfo {
    public string Identifier { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public class UserSettingsChapterPermissions {
    public string ChapterName { get; set; } = "";
    public List<UserSettingsPermissionInfo> Permissions { get; set; } = [];
}
