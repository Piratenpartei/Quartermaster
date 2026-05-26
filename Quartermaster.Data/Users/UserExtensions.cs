namespace Quartermaster.Data.Users;

public static class UserExtensions {
    /// <summary>
    /// Renders the user's display name: <c>FirstName LastName</c> when both are set,
    /// else the username, else the email. Used in greetings, login responses, and
    /// audit-log "current user" labels.
    /// </summary>
    public static string DisplayName(this User user) {
        if (!string.IsNullOrEmpty(user.FirstName) && !string.IsNullOrEmpty(user.LastName))
            return $"{user.FirstName} {user.LastName}";
        if (!string.IsNullOrEmpty(user.Username))
            return user.Username;
        return user.Email;
    }
}
