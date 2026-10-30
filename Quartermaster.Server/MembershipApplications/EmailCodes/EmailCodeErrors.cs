using System;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.MembershipApplications.EmailCodes;

public static class EmailCodeErrors {
    public static string KeyFor(EmailCodeCheckResult result) {
        return result switch {
            EmailCodeCheckResult.Wrong => I18nKey.Error.EmailCode.Wrong,
            EmailCodeCheckResult.Locked => I18nKey.Error.EmailCode.Locked,
            EmailCodeCheckResult.Expired => I18nKey.Error.EmailCode.Expired,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, null)
        };
    }
}
