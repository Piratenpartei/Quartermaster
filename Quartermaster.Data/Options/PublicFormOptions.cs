namespace Quartermaster.Data.Options;

/// <summary>
/// Option identifiers that switch the public forms on and off. Values are "true" or "false"; the motion form is
/// overridable, so a value set on a chapter also applies to its sub-chapters unless they set their own.
/// </summary>
public static class PublicFormOptions {
    public const string MembershipApplication = "public_forms.membership_application.enabled";
    public const string DueSelection = "public_forms.due_selection.enabled";
    public const string Motion = "public_forms.motion.enabled";
}
