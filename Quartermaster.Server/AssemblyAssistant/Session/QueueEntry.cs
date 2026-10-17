using System;
using Quartermaster.Api.AssemblyAssistant;

namespace Quartermaster.Server.AssemblyAssistant.Session;

public class QueueEntry {
    public string Key { get; }
    public QueueKind Kind { get; }
    public MemberIdentity Identity { get; set; }
    public uint Session { get; set; }
    public DateTime EnteredAt { get; }
    public long Sequence { get; }
    public bool Skipped { get; set; }
    public bool Dismissed { get; set; }
    public bool AfterListClosed { get; set; }
    public bool OrderUncertain { get; set; }
    public bool MovePending { get; set; }

    public QueueEntry(QueueKind kind, MemberIdentity identity, uint session, DateTime enteredAt, long sequence) {
        Key = KeyFor(kind, identity);
        Kind = kind;
        Identity = identity;
        Session = session;
        EnteredAt = enteredAt;
        Sequence = sequence;
    }

    public static string KeyFor(QueueKind kind, MemberIdentity identity) {
        return $"{kind}:{identity.Key}";
    }

    public bool IsSuggestable() {
        if (Skipped || Dismissed)
            return false;
        if (AfterListClosed)
            return false;

        return !MovePending;
    }

    public AssemblyQueueEntryDTO ToDto() {
        return new AssemblyQueueEntryDTO {
            Key = Key,
            Session = Session,
            Name = Identity.Name,
            EnteredAt = EnteredAt,
            Skipped = Skipped,
            Dismissed = Dismissed,
            AfterListClosed = AfterListClosed,
            OrderUncertain = OrderUncertain,
            MovePending = MovePending
        };
    }
}
