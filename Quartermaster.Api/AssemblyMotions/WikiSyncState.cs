namespace Quartermaster.Api.AssemblyMotions;

public enum WikiSyncState {
    NotMirrored = 0,
    Pending = 1,
    Synced = 2,
    Failed = 3,
    ProtectionFailed = 4
}
