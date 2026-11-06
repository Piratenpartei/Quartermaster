using Quartermaster.Api.Users;
using Quartermaster.Data.Members;

namespace Quartermaster.Data.Users;

public static class UserExtensions {
    extension(User user) {
        /// <summary>
        /// The name Quartermaster shows informally: the username when the user chose it and has one, otherwise <c>FirstName LastName</c>
        /// when both are set, else the username, else the email. Formal places use first and last name directly.
        /// </summary>
        public string DisplayName() {
            if (user.DisplayNameMode == DisplayNameMode.Username && user.HasUsername())
                return user.Username!;
            if (!string.IsNullOrEmpty(user.FirstName) && !string.IsNullOrEmpty(user.LastName))
                return $"{user.FirstName} {user.LastName}";
            if (!string.IsNullOrEmpty(user.Username))
                return user.Username;
            return user.Email;
        }

        /// <summary>The account as the session and settings show it; voting rights and open fees only when linked to a member.</summary>
        public LoginUserInfo ToLoginUserInfo(Member? member) {
            return new LoginUserInfo {
                Id = user.Id,
                Username = user.Username ?? "",
                FirstName = user.FirstName,
                LastName = user.LastName,
                DisplayName = user.DisplayName(),
                Email = user.Email,
                HasVotingRights = member?.HasVotingRights,
                OpenFeeTotal = member == null ? null : member.OpenFeeTotal
            };
        }

        /// <summary>A username of its own, not the e-mail address SSO accounts held as username before V9.1.</summary>
        public bool HasUsername() {
            return !string.IsNullOrEmpty(user.Username) && user.Username != user.Email;
        }
    }
}
