namespace Quartermaster.Api.Forwarding;

public static class ForwardingOptionKeys {
    public const string RegularMinimum = "dues.regular_minimum";
    public const string StateDecisionMinimum = "dues.state_decision_minimum";
    public const string MembershipApplicationEnabled = "forwarding.membership_application.enabled";
    public const string DueSelectionEnabled = "forwarding.due_selection.enabled";

    public const decimal DefaultRegularMinimum = 72;
    public const decimal DefaultStateDecisionMinimum = 12;

    public static bool IsSwitch(string key) {
        return key == MembershipApplicationEnabled || key == DueSelectionEnabled;
    }
}
