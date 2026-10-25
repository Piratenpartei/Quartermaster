namespace Quartermaster.Api.Forwarding;

public static class ForwardingOptionKeys {
    public const string RegularMinimum = "dues.regular_minimum";
    public const string StateDecisionMinimum = "dues.state_decision_minimum";
    public const string MembershipApplicationEnabled = "forwarding.membership_application.enabled";
    public const string DueSelectionEnabled = "forwarding.due_selection.enabled";
    public const string SageCheckMinutes = "forwarding.sage.check_minutes";

    public const decimal DefaultRegularMinimum = 72;
    public const decimal DefaultStateDecisionMinimum = 12;
    public const int DefaultSageCheckMinutes = 15;
    public const int MinimumSageCheckMinutes = 5;

    public static bool IsSwitch(string key) {
        return key == MembershipApplicationEnabled || key == DueSelectionEnabled;
    }
}
