namespace Quartermaster.Api.Forwarding;

public enum ForwardingPurpose {
    Regular = 0,
    Reduction = 1,
    Application = 2
}

public enum ForwardingMode {
    Quartermaster = 1,
    Redmine = 2
}

public enum ForwardingCaseKind {
    Application = 0,
    DueSelection = 1
}

public enum ForwardingCaseStatus {
    Open = 0,
    Done = 1,
    Failed = 2,
    ManuallyResolved = 3
}

public enum ForwardingStepKind {
    CreateTicket = 0,
    AddOutcomeNote = 1
}

public enum ForwardingStepState {
    Waiting = 0,
    Pending = 1,
    InFlight = 2,
    Done = 3,
    Failed = 4,
    Skipped = 5
}
