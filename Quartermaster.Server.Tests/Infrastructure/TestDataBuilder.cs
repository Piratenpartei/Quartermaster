using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Events;
using Quartermaster.Data.Members;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Meetings;
using Quartermaster.Data.Motions;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;
using Quartermaster.Api.Events;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Server.Tests.Infrastructure;

/// <summary>
/// Fluent helpers for seeding test data. Each method inserts one row and returns the entity.
/// Defaults are chosen to make the minimum-viable graph for tests; pass overrides to customize.
/// Use <see cref="SeedAuthenticatedUser"/> for the common case of creating a user with permissions + login token.
/// </summary>
public sealed class TestDataBuilder {
    private readonly DbContext _db;
    private Guid? _nullIslandId;

    public TestDataBuilder(DbContext db) {
        _db = db;
    }

    /// <summary>
    /// Returns (or creates) the "Null Island" admin division used to satisfy FK constraints
    /// for users who don't have a real citizenship/address division.
    /// </summary>
    public Guid NullIslandAdminDivisionId {
        get {
            if (_nullIslandId.HasValue)
                return _nullIslandId.Value;
            var id = Guid.NewGuid();
            _db.Insert(new AdministrativeDivision {
                Id = id,
                Name = "NullIsland",
                Depth = 0,
                ParentId = null
            });
            _nullIslandId = id;
            return id;
        }
    }

    public AdministrativeDivision SeedAdminDivision(
        string name = "Test Division",
        int depth = 0,
        Guid? parentId = null,
        string? adminCode = null,
        string? postCodes = null,
        bool isOrphaned = false) {
        var div = new AdministrativeDivision {
            Id = Guid.NewGuid(),
            Name = name,
            Depth = depth,
            ParentId = parentId,
            AdminCode = adminCode,
            PostCodes = postCodes,
            IsOrphaned = isOrphaned
        };
        _db.Insert(div);
        return div;
    }

    public Chapter SeedChapter(
        string name = "Test Chapter",
        Guid? parentChapterId = null,
        Guid? adminDivisionId = null,
        string? shortCode = null,
        string? externalCode = null) {
        var chapter = new Chapter {
            Id = Guid.NewGuid(),
            Name = name,
            ParentChapterId = parentChapterId,
            AdministrativeDivisionId = adminDivisionId,
            ShortCode = shortCode,
            ExternalCode = externalCode ?? "EC_" + Guid.NewGuid().ToString("N")[..6]
        };
        _db.Insert(chapter);
        return chapter;
    }

    /// <summary>
    /// Seeds a chain of chapters: root → child → grandchild, etc.
    /// Returns the chain in order from root to leaf.
    /// </summary>
    public List<Chapter> SeedChapterHierarchy(params string[] names) {
        if (names.Length == 0)
            throw new ArgumentException("At least one chapter name required", nameof(names));
        var chain = new List<Chapter>();
        Guid? parentId = null;
        foreach (var name in names) {
            var ch = SeedChapter(name, parentId);
            chain.Add(ch);
            parentId = ch.Id;
        }
        return chain;
    }

    public User SeedUser(
        string? email = null,
        string? password = null,
        string? username = null,
        string firstName = "Test",
        string lastName = "User",
        int memberNumber = 0) {
        var user = new User {
            Id = Guid.NewGuid(),
            EMail = email ?? $"user_{Guid.NewGuid():N}@test.local",
            Username = username,
            PasswordHash = password != null ? PasswordHashser.Hash(password) : null,
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = new DateTime(1990, 1, 1),
            CitizenshipAdministrativeDivisionId = NullIslandAdminDivisionId,
            AddressAdministrativeDivisionId = NullIslandAdminDivisionId,
            MemberSince = DateTime.UtcNow.Date,
            MemberNumber = memberNumber == 0 ? Random.Shared.Next(10000, 999999) : memberNumber,
            AddressStreet = "Teststr.",
            AddressHouseNbr = "1"
        };
        _db.Insert(user);
        return user;
    }

    /// <summary>
    /// Seeds a login token for <paramref name="userId"/> and returns the raw token string
    /// that a client would send via <c>Authorization: Bearer &lt;token&gt;</c>.
    /// Uses the server-side <see cref="TokenExtensions.LoginUser"/> which already hashes the fingerprint.
    /// The fingerprint is fixed ("test") for deterministic tests.
    /// </summary>
    public string SeedLoginToken(Guid userId, string fingerprint = "") {
        // Fingerprint MUST be "" to match TokenRepository.ValidateLoginToken (which uses empty).
        var token = _db.LoginUser(userId, fingerprint);
        // LoginUser returns the Token with Content set to the user-visible random string.
        return token.Content;
    }

    public Permission SeedPermission(string identifier, string displayName, bool global) {
        var perm = new Permission {
            Id = Guid.NewGuid(),
            Identifier = identifier,
            DisplayName = displayName,
            Global = global
        };
        _db.Insert(perm);
        return perm;
    }

    /// <summary>
    /// Looks up (or seeds) a permission by identifier and grants it to the user globally.
    /// </summary>
    public void GrantGlobalPermission(Guid userId, string permissionIdentifier) {
        var perm = _db.Permissions.FirstOrDefault(p => p.Identifier == permissionIdentifier)
            ?? SeedPermission(permissionIdentifier, permissionIdentifier, global: true);
        _db.Insert(new UserGlobalPermission {
            UserId = userId,
            PermissionId = perm.Id
        });
    }

    public void GrantChapterPermission(Guid userId, Guid chapterId, string permissionIdentifier) {
        var perm = _db.Permissions.FirstOrDefault(p => p.Identifier == permissionIdentifier)
            ?? SeedPermission(permissionIdentifier, permissionIdentifier, global: false);
        _db.Insert(new UserChapterPermission {
            UserId = userId,
            ChapterId = chapterId,
            PermissionId = perm.Id
        });
    }

    public Member SeedMember(
        Guid? chapterId = null,
        int memberNumber = 0,
        string firstName = "Test",
        string lastName = "Member",
        string? email = null,
        DateTime? entryDate = null,
        DateTime? exitDate = null,
        bool isPending = false,
        Guid? userId = null) {
        var member = new Member {
            Id = Guid.NewGuid(),
            MemberNumber = memberNumber == 0 ? Random.Shared.Next(10000, 999999) : memberNumber,
            FirstName = firstName,
            LastName = lastName,
            EMail = email,
            ChapterId = chapterId,
            EntryDate = entryDate ?? DateTime.UtcNow.Date.AddYears(-1),
            ExitDate = exitDate,
            IsPending = isPending,
            UserId = userId,
            LastImportedAt = DateTime.UtcNow
        };
        _db.Insert(member);
        return member;
    }

    public Event SeedEvent(
        Guid chapterId,
        string? internalName = null,
        string? publicName = null,
        EventStatus status = EventStatus.Draft,
        EventVisibility visibility = EventVisibility.Private,
        DateTime? eventDate = null,
        string? description = null) {
        var ev = new Event {
            Id = Guid.NewGuid(),
            ChapterId = chapterId,
            InternalName = internalName ?? "Test Event",
            PublicName = publicName ?? "Public Test Event",
            Description = description,
            EventDate = eventDate,
            Status = status,
            Visibility = visibility,
            CreatedAt = DateTime.UtcNow
        };
        _db.Insert(ev);
        return ev;
    }

    public EventTemplate SeedEventTemplate(
        string name = "Test Template",
        Guid? chapterId = null) {
        var tpl = new EventTemplate {
            Id = Guid.NewGuid(),
            Name = name,
            PublicNameTemplate = "{{name}}",
            ChapterId = chapterId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Insert(tpl);
        return tpl;
    }

    public EventChecklistItem SeedChecklistItem(
        Guid eventId,
        int sortOrder = 0,
        string label = "Item",
        ChecklistItemType itemType = ChecklistItemType.Text,
        bool isCompleted = false) {
        var item = new EventChecklistItem {
            Id = Guid.NewGuid(),
            EventId = eventId,
            SortOrder = sortOrder,
            Label = label,
            ItemType = itemType,
            IsCompleted = isCompleted,
            CompletedAt = isCompleted ? DateTime.UtcNow : null
        };
        _db.Insert(item);
        return item;
    }

    public Motion SeedMotion(
        Guid chapterId,
        string title = "Test Motion",
        string text = "Motion text",
        MotionApprovalStatus status = MotionApprovalStatus.Pending,
        string authorName = "Author",
        string authorEmail = "author@test.local",
        bool isRealized = false) {
        var motion = new Motion {
            Id = Guid.NewGuid(),
            ChapterId = chapterId,
            Title = title,
            Text = text,
            ApprovalStatus = status,
            AuthorName = authorName,
            AuthorEMail = authorEmail,
            IsRealized = isRealized,
            CreatedAt = DateTime.UtcNow
        };
        _db.Insert(motion);
        return motion;
    }

    public MotionVote SeedMotionVote(Guid motionId, Guid userId, VoteType vote) {
        var v = new MotionVote {
            Id = Guid.NewGuid(),
            MotionId = motionId,
            UserId = userId,
            Vote = vote,
            VotedAt = DateTime.UtcNow
        };
        _db.Insert(v);
        return v;
    }

    public MembershipApplication SeedMembershipApplication(
        Guid? chapterId = null,
        string firstName = "Applicant",
        string lastName = "Test",
        string email = "applicant@test.local",
        ApplicationStatus status = ApplicationStatus.Pending) {
        var app = new MembershipApplication {
            Id = Guid.NewGuid(),
            ChapterId = chapterId,
            FirstName = firstName,
            LastName = lastName,
            EMail = email,
            Status = status,
            DateOfBirth = new DateTime(1990, 1, 1),
            Citizenship = "DE",
            PhoneNumber = "0123456789",
            AddressStreet = "Teststr.",
            AddressHouseNbr = "1",
            AddressPostCode = "12345",
            AddressCity = "Teststadt",
            ConformityDeclarationAccepted = true,
            ApplicationText = "Test",
            EntryDate = DateTime.UtcNow.Date,
            SubmittedAt = DateTime.UtcNow
        };
        _db.Insert(app);
        return app;
    }

    public DueSelection SeedDueSelection(
        string firstName = "Due",
        string lastName = "Tester",
        int? memberNumber = null,
        DueSelectionStatus status = DueSelectionStatus.Pending) {
        var due = new DueSelection {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            MemberNumber = memberNumber,
            Status = status,
            SelectedDue = 10m,
            YearlyIncome = 30000m,
            MonthlyIncomeGroup = 2500m,
            ReducedAmount = 0m,
            ReducedJustification = "",
            AccountHolder = firstName + " " + lastName,
            IBAN = "DE89370400440532013000"
        };
        _db.Insert(due);
        return due;
    }

    public ChapterOfficer SeedChapterOfficer(
        Guid memberId,
        Guid chapterId,
        ChapterOfficerType officerType = ChapterOfficerType.Member) {
        var officer = new ChapterOfficer {
            MemberId = memberId,
            ChapterId = chapterId,
            AssociateType = officerType
        };
        _db.Insert(officer);
        return officer;
    }

    public Role SeedRole(
        string identifier,
        string name,
        RoleScope scope,
        bool isSystem = false,
        string description = "") {
        var role = new Role {
            Id = Guid.NewGuid(),
            Identifier = identifier,
            Name = name,
            Description = description,
            Scope = scope,
            IsSystem = isSystem
        };
        _db.Insert(role);
        return role;
    }

    public void AssignRoleToUser(Guid userId, Guid roleId, Guid? chapterId = null) {
        _db.Insert(new UserRoleAssignment {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = roleId,
            ChapterId = chapterId
        });
    }

    public Meeting SeedMeeting(
        Guid chapterId,
        string title = "Test Sitzung",
        MeetingStatus status = MeetingStatus.Scheduled,
        MeetingVisibility visibility = MeetingVisibility.Private,
        DateTime? meetingDate = null,
        string? location = null,
        string? description = null) {
        var meeting = new Meeting {
            Id = Guid.NewGuid(),
            ChapterId = chapterId,
            Title = title,
            Status = status,
            Visibility = visibility,
            MeetingDate = meetingDate,
            Location = location,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
        _db.Insert(meeting);
        return meeting;
    }

    public AgendaItem SeedAgendaItem(
        Guid meetingId,
        Guid? parentId = null,
        string title = "TOP",
        AgendaItemType itemType = AgendaItemType.Discussion,
        Guid? motionId = null,
        int sortOrder = 0) {
        var item = new AgendaItem {
            Id = Guid.NewGuid(),
            MeetingId = meetingId,
            ParentId = parentId,
            SortOrder = sortOrder,
            Title = title,
            ItemType = itemType,
            MotionId = motionId
        };
        _db.Insert(item);
        return item;
    }

    public void AddPermissionToRole(Guid roleId, string permissionIdentifier) {
        _db.Insert(new RolePermission {
            RoleId = roleId,
            PermissionIdentifier = permissionIdentifier
        });
    }

    /// <summary>
    /// Creates a user with the given global and chapter permissions, seeds a login token,
    /// and returns both the user entity and the raw bearer token.
    /// </summary>
    public (User User, string Token) SeedAuthenticatedUser(
        string[]? globalPermissions = null,
        Dictionary<Guid, string[]>? chapterPermissions = null,
        string? email = null,
        string? password = null,
        string firstName = "Test",
        string lastName = "User") {
        var user = SeedUser(email: email, password: password, firstName: firstName, lastName: lastName);

        if (globalPermissions != null) {
            foreach (var perm in globalPermissions) {
                GrantGlobalPermission(user.Id, perm);
            }
        }
        if (chapterPermissions != null) {
            foreach (var (chapterId, perms) in chapterPermissions) {
                foreach (var perm in perms) {
                    GrantChapterPermission(user.Id, chapterId, perm);
                }
            }
        }

        var token = SeedLoginToken(user.Id);
        return (user, token);
    }
}
