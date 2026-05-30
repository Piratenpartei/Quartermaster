namespace Quartermaster.Api.I18n;

/// <summary>
/// Stable string identifiers for all translatable messages returned by the API.
/// Both server (producers) and client (consumers) reference these constants so
/// the wire format stays decoupled from display language.
///
/// Key naming convention: <c>error.&lt;feature&gt;.&lt;specific_context&gt;</c> in
/// snake_case. Keys are grouped into nested static classes for discoverability.
///
/// When adding a new key, also add its German translation to
/// <c>Quartermaster.Api/I18n/de.json</c>.
/// </summary>
public static class I18nKey {
    public static class Error {
        public static class Validation {
            public const string PageMin = "error.validation.common.page_min";
            public const string PageSizeRange = "error.validation.common.page_size_range";
        }

        public static class User {
            public const string DeleteSelfForbidden = "error.user.delete_self_forbidden";

            public static class Login {
                public const string UsernameOrEmailRequired = "error.user.login.username_or_email_required";
                public const string PasswordMinLength = "error.user.login.password_min_length";
                public const string UnlockIpAndUsernameRequired = "error.user.login.unlock_ip_and_username_required";
            }

            public static class Role {
                public const string SystemNotEditable = "error.user.role.system_not_editable";
                public const string SystemNotDeletable = "error.user.role.system_not_deletable";
                public const string NameRequired = "error.user.role.name_required";
                public const string ScopeInvalid = "error.user.role.scope_invalid";
                public const string HasActiveAssignments = "error.user.role.has_active_assignments";
            }

            public static class RoleAssignment {
                public const string RoleNotFound = "error.user.role_assignment.role_not_found";
                public const string UserNotFound = "error.user.role_assignment.user_not_found";
                public const string ChapterRequired = "error.user.role_assignment.chapter_required";
                public const string ChapterNotFound = "error.user.role_assignment.chapter_not_found";
                public const string GlobalNoChapter = "error.user.role_assignment.global_no_chapter";
            }
        }

        public static class Chapter {
            public const string NameRequired = "error.chapter.name_required";
            public const string ParentNotFound = "error.chapter.parent_not_found";
            public const string ParentSelfReference = "error.chapter.parent_self_reference";
            public const string ExternalCodeNotUnique = "error.chapter.external_code_not_unique";
            public const string HasChildren = "error.chapter.has_children";

            public static class Officer {
                public const string MemberRequired = "error.chapter.officer.member_required";
                public const string ChapterRequired = "error.chapter.officer.chapter_required";
                public const string InvalidOfficerType = "error.chapter.officer.invalid_officer_type";
                public const string MemberNotFound = "error.chapter.officer.member_not_found";
                public const string MemberChapterMismatch = "error.chapter.officer.member_chapter_mismatch";
            }
        }

        public static class Email {
            public static class Test {
                public const string RecipientInvalid = "error.email.test.recipient_invalid";
            }
        }

        public static class Motion {
            public const string ChapterRequired = "error.motion.chapter_required";
            public const string ChapterNotFound = "error.motion.chapter_not_found";
            public const string SubmitterNameRequired = "error.motion.submitter_name_required";
            public const string SubmitterNameMaxLength = "error.motion.submitter_name_max_length";
            public const string EmailRequired = "error.motion.email_required";
            public const string EmailInvalid = "error.motion.email_invalid";
            public const string EmailMaxLength = "error.motion.email_max_length";
            public const string TitleRequired = "error.motion.title_required";
            public const string TitleMaxLength = "error.motion.title_max_length";
            public const string BodyRequired = "error.motion.body_required";
            public const string BodyMaxLength = "error.motion.body_max_length";

            public static class Update {
                public const string LockedAfterDecision = "error.motion.update.locked_after_decision";
                public const string LinkedApplicationNotFound = "error.motion.update.linked_application_not_found";
                public const string LinkedDueSelectionNotFound = "error.motion.update.linked_due_selection_not_found";
            }

            public static class Vote {
                public const string MotionIdRequired = "error.motion.vote.motion_id_required";
                public const string MemberIdRequired = "error.motion.vote.member_id_required";
                public const string InvalidVote = "error.motion.vote.invalid_vote";
                public const string TargetNotOfficer = "error.motion.vote.target_not_officer";
                public const string NoProxyPermission = "error.motion.vote.no_proxy_permission";
            }

            public static class Status {
                public const string MotionIdRequired = "error.motion.status.motion_id_required";
            }
        }

        public static class Meeting {
            public const string ChapterRequired = "error.meeting.chapter_required";
            public const string TitleRequired = "error.meeting.title_required";
            public const string TitleMaxLength = "error.meeting.title_max_length";
            public const string LocationMaxLength = "error.meeting.location_max_length";
            public const string DescriptionMaxLength = "error.meeting.description_max_length";
            public const string VisibilityInvalid = "error.meeting.visibility_invalid";
            public const string ProtocolNotAvailable = "error.meeting.protocol_not_available";
            public const string ProtocolUnknownFormat = "error.meeting.protocol_unknown_format";

            public static class Status {
                public const string Invalid = "error.meeting.status.invalid";
                public const string TransitionInvalid = "error.meeting.status.transition_invalid";
                public const string DateRequiredForScheduled = "error.meeting.status.date_required_for_scheduled";
            }

            public static class Agenda {
                public const string MeetingRequired = "error.meeting.agenda.meeting_required";
                public const string ItemRequired = "error.meeting.agenda.item_required";
                public const string TitleRequired = "error.meeting.agenda.title_required";
                public const string TitleMaxLength = "error.meeting.agenda.title_max_length";
                public const string ItemTypeInvalid = "error.meeting.agenda.item_type_invalid";
                public const string NotesMaxLength = "error.meeting.agenda.notes_max_length";
                public const string ResolutionMaxLength = "error.meeting.agenda.resolution_max_length";
                public const string ReorderDirectionInvalid = "error.meeting.agenda.reorder_direction_invalid";
                public const string VoteRequiresInProgress = "error.meeting.agenda.vote_requires_in_progress";
                public const string StartRequiresInProgress = "error.meeting.agenda.start_requires_in_progress";
                public const string CompleteRequiresInProgress = "error.meeting.agenda.complete_requires_in_progress";
                public const string ReopenRequiresInProgress = "error.meeting.agenda.reopen_requires_in_progress";
                public const string CloseVoteRequiresInProgress = "error.meeting.agenda.close_vote_requires_in_progress";
                public const string PresenceRequiresInProgress = "error.meeting.agenda.presence_requires_in_progress";
                public const string DeleteStatusInvalid = "error.meeting.agenda.delete_status_invalid";
                public const string NotMotionItem = "error.meeting.agenda.not_motion_item";
                public const string NotPresenceItem = "error.meeting.agenda.not_presence_item";
                public const string VoteTargetNotOfficer = "error.meeting.agenda.vote_target_not_officer";
                public const string VoteNoProxyPermission = "error.meeting.agenda.vote_no_proxy_permission";
                public const string VoteValueInvalid = "error.meeting.agenda.vote_value_invalid";
                public const string MotionLinkRequired = "error.meeting.agenda.motion_link_required";
                public const string LinkedMotionNotFound = "error.meeting.agenda.linked_motion_not_found";
                public const string MotionChapterMismatch = "error.meeting.agenda.motion_chapter_mismatch";
                public const string ParentNotInMeeting = "error.meeting.agenda.parent_not_in_meeting";
                public const string NewParentNotInMeeting = "error.meeting.agenda.new_parent_not_in_meeting";
                public const string MoveWouldCycle = "error.meeting.agenda.move_would_cycle";
                public const string MaxDepthExceeded = "error.meeting.agenda.max_depth_exceeded";
            }
        }

        public static class Event {
            public const string IdRequired = "error.event.id_required";
            public const string ChapterRequired = "error.event.chapter_required";
            public const string InternalNameRequired = "error.event.internal_name_required";
            public const string InternalNameMaxLength = "error.event.internal_name_max_length";
            public const string PublicNameRequired = "error.event.public_name_required";
            public const string PublicNameMaxLength = "error.event.public_name_max_length";

            public static class Status {
                public const string TransitionInvalid = "error.event.status.transition_invalid";
            }

            public static class Template {
                public const string TemplateRequired = "error.event.template.template_required";
                public const string ChapterRequired = "error.event.template.chapter_required";
                public const string EventRequired = "error.event.template.event_required";
                public const string NameRequired = "error.event.template.name_required";
                public const string NameMaxLength = "error.event.template.name_max_length";
                public const string OnlyFromDraft = "error.event.template.only_from_draft";
            }

            public static class Checklist {
                public const string EventRequired = "error.event.checklist.event_required";
                public const string ItemIdRequired = "error.event.checklist.item_id_required";
                public const string LabelRequired = "error.event.checklist.label_required";
                public const string LabelMaxLength = "error.event.checklist.label_max_length";
                public const string TypeInvalid = "error.event.checklist.type_invalid";
                public const string ReorderDirectionInvalid = "error.event.checklist.reorder_direction_invalid";
                public const string AlreadyCompleted = "error.event.checklist.already_completed";
                public const string OnlyTextCanBeUnchecked = "error.event.checklist.only_text_can_be_unchecked";
            }
        }

        public static class Member {
            public static class Import {
                public const string NoFileUploaded = "error.member.import.no_file_uploaded";
                public const string OnlyCsvAllowed = "error.member.import.only_csv_allowed";
                public const string FilePathNotConfigured = "error.member.import.file_path_not_configured";
            }

            public static class AdminDivision {
                public const string NotFound = "error.member.admin_division.not_found";
            }
        }

        public static class Admin {
            public static class Application {
                public const string IdRequired = "error.admin.application.id_required";
                public const string StatusInvalid = "error.admin.application.status_invalid";
                public const string FirstNameRequired = "error.admin.application.first_name_required";
                public const string FirstNameMaxLength = "error.admin.application.first_name_max_length";
                public const string LastNameRequired = "error.admin.application.last_name_required";
                public const string LastNameMaxLength = "error.admin.application.last_name_max_length";
                public const string EmailRequired = "error.admin.application.email_required";
                public const string EmailInvalid = "error.admin.application.email_invalid";
                public const string EmailMaxLength = "error.admin.application.email_max_length";
                public const string NationalityRequired = "error.admin.application.nationality_required";
                public const string NationalityMaxLength = "error.admin.application.nationality_max_length";
                public const string PhoneMaxLength = "error.admin.application.phone_max_length";
                public const string StreetRequired = "error.admin.application.street_required";
                public const string StreetMaxLength = "error.admin.application.street_max_length";
                public const string HouseNumberRequired = "error.admin.application.house_number_required";
                public const string HouseNumberMaxLength = "error.admin.application.house_number_max_length";
                public const string PostalCodeRequired = "error.admin.application.postal_code_required";
                public const string PostalCodeMaxLength = "error.admin.application.postal_code_max_length";
                public const string CityRequired = "error.admin.application.city_required";
                public const string CityMaxLength = "error.admin.application.city_max_length";
                public const string BodyMaxLength = "error.admin.application.body_max_length";
                public const string DeclarationRequired = "error.admin.application.declaration_required";
                public const string MemberNumberRequired = "error.admin.application.member_number_required";
                public const string NotApprovedForWelcome = "error.admin.application.not_approved_for_welcome";
                public const string WelcomeAlreadySent = "error.admin.application.welcome_already_sent";
                public const string NotPendingDivisionLinking = "error.admin.application.not_pending_division_linking";
                public const string DivisionRequired = "error.admin.application.division_required";
                public const string NoChapterForDivision = "error.admin.application.no_chapter_for_division";
            }

            public static class DueSelection {
                public const string IdRequired = "error.admin.due_selection.id_required";
                public const string StatusInvalid = "error.admin.due_selection.status_invalid";
                public const string FirstNameRequired = "error.admin.due_selection.first_name_required";
                public const string LastNameRequired = "error.admin.due_selection.last_name_required";
                public const string EmailInvalid = "error.admin.due_selection.email_invalid";
                public const string AmountNotNegative = "error.admin.due_selection.amount_not_negative";
                public const string AccountHolderMaxLength = "error.admin.due_selection.account_holder_max_length";
                public const string IbanMaxLength = "error.admin.due_selection.iban_max_length";
                public const string JustificationMaxLength = "error.admin.due_selection.justification_max_length";
            }

            public static class Option {
                public const string IdentifierRequired = "error.admin.option.identifier_required";
                public const string ValueMaxLength = "error.admin.option.value_max_length";
            }
        }
    }

    /// <summary>
    /// Display-language strings used by the Blazor UI: toast messages, confirm dialog
    /// prompts, default labels. Server-side i18n stays under <see cref="Error"/>.
    /// </summary>
    public static class Ui {
        public static class Toast {
            public const string Saved = "ui.toast.saved";
            public const string EventCreated = "ui.toast.event.created";
            public const string EventStatusChanged = "ui.toast.event.status_changed";
            public const string ChecklistItemAdded = "ui.toast.event.checklist_item_added";
            public const string ChecklistItemDeleted = "ui.toast.event.checklist_item_deleted";
            public const string MeetingCreated = "ui.toast.meeting.created";
            public const string MeetingDeleted = "ui.toast.meeting.deleted";
            public const string MeetingEnded = "ui.toast.meeting.ended";
            public const string MotionsImported = "ui.toast.meeting.motions_imported";
            public const string TopReopened = "ui.toast.meeting.top_reopened";
            public const string VoteEnded = "ui.toast.meeting.vote_ended";
            public const string TemplateSaved = "ui.toast.template.saved";
            public const string TemplateDeleted = "ui.toast.template.deleted";
            public const string MemberImportCompleted = "ui.toast.member_import.completed";
            public const string AdminDivisionAssigned = "ui.toast.member.admin_division_assigned";
            public const string OfficerAdded = "ui.toast.officer.added";
            public const string LockoutReleased = "ui.toast.lockout.released";
            public const string UserDeleted = "ui.toast.user.deleted";
            public const string RoleCreated = "ui.toast.role.created";
            public const string RoleDeleted = "ui.toast.role.deleted";
            public const string AssignmentCreated = "ui.toast.assignment.created";
            public const string AssignmentRemoved = "ui.toast.assignment.removed";
            public const string MotionCreated = "ui.toast.motion.created";
            public const string MotionStatusUpdated = "ui.toast.motion.status_updated";
            public const string MotionMarkedRealized = "ui.toast.motion.marked_realized";
            public const string TestdataCreated = "ui.toast.testdata.created";
            public const string DueSelectionThanks = "ui.toast.due_selection.thanks";
            public const string MembershipApplicationSubmitted = "ui.toast.membership_application.submitted";
            public const string PublicMotionSubmitted = "ui.toast.motion.public_submitted";
            public const string LoginFailedCredentials = "ui.toast.login.failed_credentials";
            public const string LoginFailedGeneric = "ui.toast.login.failed_generic";
            public const string SessionRevoked = "ui.toast.session.revoked";
            public const string SessionOthersRevoked = "ui.toast.session.others_revoked";
            public const string WelcomeMailSent = "ui.toast.membership_application.welcome_sent";
            public const string DivisionLinked = "ui.toast.membership_application.division_linked";
        }

        public static class Error {
            public const string Generic = "ui.error.generic";
            public const string UserRequired = "ui.error.user_required";
            public const string RoleRequired = "ui.error.role_required";
            public const string ChapterRequired = "ui.error.chapter_required";
            public const string CsvOnly = "ui.error.csv_only";
            public const string ChapterAndTitleRequired = "ui.error.chapter_and_title_required";
            public const string NameRequired = "ui.error.name_required";
        }

        public static class Confirm {
            public const string DefaultTitle = "ui.confirm.default_title";
            public const string DefaultMessage = "ui.confirm.default_message";
            public const string DefaultButton = "ui.confirm.default_button";
            public const string DefaultCancel = "ui.confirm.default_cancel";
            public const string ChecklistItemDelete = "ui.confirm.checklist_item_delete";
            public const string AgendaItemDelete = "ui.confirm.agenda_item_delete";
            public const string MeetingDelete = "ui.confirm.meeting_delete";
            public const string TemplateDelete = "ui.confirm.template_delete";
            public const string EventArchive = "ui.confirm.event.archive";
            public const string EventBackToDraft = "ui.confirm.event.back_to_draft";
            public const string MeetingFinish = "ui.confirm.meeting_finish";
            public const string SessionRevokeCurrent = "ui.confirm.session.revoke_current";
            public const string SessionRevokeOthers = "ui.confirm.session.revoke_others";
        }

        public static class Label {
            public const string BackToOverview = "ui.label.back_to_overview";
            public const string EventStatusDraft = "ui.label.event_status.draft";
            public const string EventStatusActive = "ui.label.event_status.active";
            public const string EventStatusCompleted = "ui.label.event_status.completed";
            public const string EventStatusArchived = "ui.label.event_status.archived";
        }

        public static class Common {
            public const string Save = "ui.common.save";
            public const string Cancel = "ui.common.cancel";
            public const string Edit = "ui.common.edit";
            public const string Delete = "ui.common.delete";
            public const string Add = "ui.common.add";
            public const string Create = "ui.common.create";
            public const string Update = "ui.common.update";
            public const string Confirm = "ui.common.confirm";
            public const string Submit = "ui.common.submit";
            public const string Close = "ui.common.close";
            public const string Back = "ui.common.back";
            public const string Next = "ui.common.next";
            public const string Continue = "ui.common.continue";
            public const string Search = "ui.common.search";
            public const string Apply = "ui.common.apply";
            public const string Yes = "ui.common.yes";
            public const string No = "ui.common.no";
            public const string Loading = "ui.common.loading";
            public const string NoEntries = "ui.common.no_entries";
            public const string NotFound = "ui.common.not_found";
            public const string System = "ui.common.system";
            public const string Name = "ui.common.name";
            public const string Title = "ui.common.title";
            public const string Email = "ui.common.email";
            public const string Phone = "ui.common.phone";
            public const string Date = "ui.common.date";
            public const string DateTime = "ui.common.datetime";
            public const string Status = "ui.common.status";
            public const string Action = "ui.common.action";
            public const string Actions = "ui.common.actions";
            public const string Role = "ui.common.role";
            public const string Created = "ui.common.created";
            public const string Modified = "ui.common.modified";
            public const string Chapter = "ui.common.chapter";
            public const string Visibility = "ui.common.visibility";
            public const string Public = "ui.common.public";
            public const string NotPublic = "ui.common.not_public";
            public const string Details = "ui.common.details";
            public const string Management = "ui.common.management";
            public const string Notes = "ui.common.notes";
            public const string Description = "ui.common.description";
            public const string FirstName = "ui.common.first_name";
            public const string LastName = "ui.common.last_name";
            public const string Address = "ui.common.address";
            public const string Member = "ui.common.member";
            public const string Officer = "ui.common.officer";
            public const string Required = "ui.common.required";
            public const string Optional = "ui.common.optional";
        }

        public static class MotionStatus {
            public const string Pending = "ui.motion_status.pending";
            public const string Approved = "ui.motion_status.approved";
            public const string Rejected = "ui.motion_status.rejected";
            public const string FormallyRejected = "ui.motion_status.formally_rejected";
            public const string ClosedWithoutAction = "ui.motion_status.closed_without_action";
            public const string Unknown = "ui.motion_status.unknown";
            public const string All = "ui.motion_status.all";
        }

        public static class MotionList {
            public const string PageTitle = "ui.motion_list.page_title";
            public const string NewMotion = "ui.motion_list.new_motion";
            public const string TotalCount = "ui.motion_list.total_count";
            public const string AuthorColumn = "ui.motion_list.author_column";
            public const string RealizedColumn = "ui.motion_list.realized_column";
        }

        public static class MotionCreate {
            public const string AdminPageTitle = "ui.motion_create.admin_page_title";
            public const string PublicPageTitle = "ui.motion_create.public_page_title";
            public const string AuthorNameLabel = "ui.motion_create.author_name_label";
            public const string AuthorEmailLabel = "ui.motion_create.author_email_label";
            public const string YourName = "ui.motion_create.your_name";
            public const string YourEmail = "ui.motion_create.your_email";
            public const string MotionTitleLabel = "ui.motion_create.motion_title_label";
            public const string BodyLabel = "ui.motion_create.body_label";
            public const string BodyMarkdownLabel = "ui.motion_create.body_markdown_label";
            public const string AuthedPrefillNotice = "ui.motion_create.authed_prefill_notice";
            public const string AuthedPrefillShort = "ui.motion_create.authed_prefill_short";
            public const string Creating = "ui.motion_create.creating";
            public const string SubmitAdmin = "ui.motion_create.submit_admin";
            public const string SubmitPublic = "ui.motion_create.submit_public";
            public const string SubmittedDirectSuccess = "ui.motion_create.submitted_direct_success";
        }

        public static class OfficerRole {
            public const string Captain = "ui.officer_role.captain";
            public const string FirstOfficer = "ui.officer_role.first_officer";
            public const string Quartermaster = "ui.officer_role.quartermaster";
            public const string Treasurer = "ui.officer_role.treasurer";
            public const string ViceTreasurer = "ui.officer_role.vice_treasurer";
            public const string PoliticalDirector = "ui.officer_role.political_director";
            public const string Member = "ui.officer_role.member";
        }

        public static class MotionDetail {
            public const string NotFound = "ui.motion_detail.not_found";
            public const string RealizedBadge = "ui.motion_detail.realized_badge";
            public const string EditCardTitle = "ui.motion_detail.edit_card_title";
            public const string AuthorNameLabel = "ui.motion_detail.author_name_label";
            public const string AuthorEmailLabel = "ui.motion_detail.author_email_label";
            public const string BodyMarkdownLabel = "ui.motion_detail.body_markdown_label";
            public const string AuthorLabel = "ui.motion_detail.author_label";
            public const string PiiWarning = "ui.motion_detail.pii_warning";
            public const string MakePrivate = "ui.motion_detail.make_private";
            public const string MakePublic = "ui.motion_detail.make_public";
            public const string ResolvedLabel = "ui.motion_detail.resolved_label";
            public const string LinkLabel = "ui.motion_detail.link_label";
            public const string ViewApplication = "ui.motion_detail.view_application";
            public const string ViewDueSelection = "ui.motion_detail.view_due_selection";
            public const string BodyLabel = "ui.motion_detail.body_label";
            public const string VotesTitle = "ui.motion_detail.votes_title";
            public const string VoteColumn = "ui.motion_detail.vote_column";
            public const string CastVoteColumn = "ui.motion_detail.cast_vote_column";
            public const string VoteApprove = "ui.motion_detail.vote_approve";
            public const string VoteDeny = "ui.motion_detail.vote_deny";
            public const string VoteAbstain = "ui.motion_detail.vote_abstain";
            public const string AriaApprove = "ui.motion_detail.aria_approve";
            public const string AriaDeny = "ui.motion_detail.aria_deny";
            public const string AriaAbstain = "ui.motion_detail.aria_abstain";
            public const string FormallyReject = "ui.motion_detail.formally_reject";
            public const string CloseNoDecision = "ui.motion_detail.close_no_decision";
            public const string MarkRealized = "ui.motion_detail.mark_realized";
            public const string AuditHistory = "ui.motion_detail.audit_history";
            public const string AuditTimestamp = "ui.motion_detail.audit_timestamp";
            public const string AuditActor = "ui.motion_detail.audit_actor";
            public const string AuditAction = "ui.motion_detail.audit_action";
            public const string AuditField = "ui.motion_detail.audit_field";
            public const string AuditBefore = "ui.motion_detail.audit_before";
            public const string AuditAfter = "ui.motion_detail.audit_after";
            public const string FieldTitle = "ui.motion_detail.field.title";
            public const string FieldBody = "ui.motion_detail.field.body";
            public const string FieldAuthorName = "ui.motion_detail.field.author_name";
            public const string FieldAuthorEmail = "ui.motion_detail.field.author_email";
            public const string FieldLinkedApplication = "ui.motion_detail.field.linked_application";
            public const string FieldLinkedDueSelection = "ui.motion_detail.field.linked_due_selection";
            public const string FieldStatus = "ui.motion_detail.field.status";
            public const string FieldRealized = "ui.motion_detail.field.realized";
            public const string FieldVisibility = "ui.motion_detail.field.visibility";
            public const string ActionCreated = "ui.motion_detail.action.created";
            public const string ActionUpdated = "ui.motion_detail.action.updated";
            public const string ActionDeleted = "ui.motion_detail.action.deleted";
        }

        public static class MainNavBar {
            public const string Home = "ui.main_nav_bar.home";
            public const string MemberPortal = "ui.main_nav_bar.member_portal";
            public const string MemberPortalDueSelector = "ui.main_nav_bar.member_portal.due_selector";
            public const string MemberPortalApplication = "ui.main_nav_bar.member_portal.application";
            public const string MemberPortalSubmitMotion = "ui.main_nav_bar.member_portal.submit_motion";
            public const string BoardWork = "ui.main_nav_bar.board_work";
            public const string BoardWorkMotions = "ui.main_nav_bar.board_work.motions";
            public const string BoardWorkMeetings = "ui.main_nav_bar.board_work.meetings";
            public const string BoardWorkApplications = "ui.main_nav_bar.board_work.applications";
            public const string BoardWorkDueSelections = "ui.main_nav_bar.board_work.due_selections";
            public const string BoardWorkMembers = "ui.main_nav_bar.board_work.members";
            public const string BoardWorkEvents = "ui.main_nav_bar.board_work.events";
            public const string BoardWorkEventTemplates = "ui.main_nav_bar.board_work.event_templates";
            public const string Administration = "ui.main_nav_bar.administration";
            public const string AdministrationDivisionsSearch = "ui.main_nav_bar.administration.divisions_search";
            public const string AdministrationDivisionsTree = "ui.main_nav_bar.administration.divisions_tree";
            public const string System = "ui.main_nav_bar.system";
            public const string SystemSettings = "ui.main_nav_bar.system.settings";
            public const string SystemTemplates = "ui.main_nav_bar.system.templates";
            public const string SystemUsers = "ui.main_nav_bar.system.users";
            public const string SystemLoginLockouts = "ui.main_nav_bar.system.login_lockouts";
            public const string SystemRoles = "ui.main_nav_bar.system.roles";
            public const string SystemChaptersList = "ui.main_nav_bar.system.chapters_list";
            public const string SystemChaptersTree = "ui.main_nav_bar.system.chapters_tree";
            public const string SystemChapterOfficers = "ui.main_nav_bar.system.chapter_officers";
            public const string SystemMembers = "ui.main_nav_bar.system.members";
            public const string SystemMemberImport = "ui.main_nav_bar.system.member_import";
            public const string SystemImportStatus = "ui.main_nav_bar.system.import_status";
            public const string MySessionsTitle = "ui.main_nav_bar.my_sessions_title";
            public const string NotificationsTitle = "ui.main_nav_bar.notifications_title";
            public const string Logout = "ui.main_nav_bar.logout";
            public const string Login = "ui.main_nav_bar.login";
        }

        public static class Login {
            public const string PageTitle = "ui.login.page_title";
            public const string SsoSubtitle = "ui.login.sso_subtitle";
            public const string OpenIdSubtitle = "ui.login.openid_subtitle";
            public const string SsoNotConfiguredTooltip = "ui.login.sso_not_configured_tooltip";
            public const string SsoTitle = "ui.login.sso_title";
            public const string NotAvailable = "ui.login.not_available";
            public const string ManualTitle = "ui.login.manual_title";
            public const string ManualSubtitle = "ui.login.manual_subtitle";
            public const string ErrorSamlNoMemberWithSupport = "ui.login.error.saml_no_member_with_support";
            public const string ErrorSamlNoMemberNoSupport = "ui.login.error.saml_no_member_no_support";
            public const string ErrorSamlMemberExited = "ui.login.error.saml_member_exited";
            public const string ErrorSamlInvalid = "ui.login.error.saml_invalid";
            public const string ErrorSamlSignature = "ui.login.error.saml_signature";
            public const string ErrorSamlNoIdentity = "ui.login.error.saml_no_identity";
            public const string ErrorOidcIdpError = "ui.login.error.oidc_idp_error";
            public const string ErrorOidcNoCode = "ui.login.error.oidc_no_code";
            public const string ErrorOidcNotConfigured = "ui.login.error.oidc_not_configured";
            public const string ErrorOidcExpired = "ui.login.error.oidc_expired";
            public const string ErrorOidcExchangeFailed = "ui.login.error.oidc_exchange_failed";
            public const string ErrorOidcNoIdToken = "ui.login.error.oidc_no_id_token";
            public const string ErrorOidcInvalidToken = "ui.login.error.oidc_invalid_token";
            public const string ErrorSsoGeneric = "ui.login.error.sso_generic";
        }

        public static class UserSettings {
            public const string PageTitle = "ui.user_settings.page_title";
            public const string NotLoggedIn = "ui.user_settings.not_logged_in";
            public const string MyAccount = "ui.user_settings.my_account";
            public const string UserDataTitle = "ui.user_settings.user_data_title";
            public const string DisplayName = "ui.user_settings.display_name";
            public const string Username = "ui.user_settings.username";
            public const string LoginMethod = "ui.user_settings.login_method";
            public const string LoginMethodSso = "ui.user_settings.login_method.sso";
            public const string LoginMethodManual = "ui.user_settings.login_method.manual";
            public const string MembershipTitle = "ui.user_settings.membership_title";
            public const string MemberNumber = "ui.user_settings.member_number";
            public const string EntryDate = "ui.user_settings.entry_date";
            public const string MembershipFee = "ui.user_settings.membership_fee";
            public const string ReducedFee = "ui.user_settings.reduced_fee";
            public const string FeeSuffix = "ui.user_settings.fee_suffix";
            public const string VotingRights = "ui.user_settings.voting_rights";
            public const string VotingRightsNoOpenFee = "ui.user_settings.voting_rights.no_open_fee";
            public const string PendingBadge = "ui.user_settings.pending_badge";
            public const string GlobalPermissionsTitle = "ui.user_settings.global_permissions_title";
            public const string NoGlobalPermissions = "ui.user_settings.no_global_permissions";
            public const string ChapterPermissionsTitle = "ui.user_settings.chapter_permissions_title";
            public const string NoChapterPermissions = "ui.user_settings.no_chapter_permissions";
            public const string DevelopmentTitle = "ui.user_settings.development_title";
            public const string DevelopmentNotice = "ui.user_settings.development_notice";
            public const string CreateTestData = "ui.user_settings.create_test_data";
        }

        public static class UserSessions {
            public const string PageTitle = "ui.user_sessions.page_title";
            public const string LogOutAllOthers = "ui.user_sessions.log_out_all_others";
            public const string Description = "ui.user_sessions.description";
            public const string NoSessions = "ui.user_sessions.no_sessions";
            public const string DeviceBrowserColumn = "ui.user_sessions.column.device_browser";
            public const string IpAddressColumn = "ui.user_sessions.column.ip_address";
            public const string SignedInSinceColumn = "ui.user_sessions.column.signed_in_since";
            public const string ExpiresColumn = "ui.user_sessions.column.expires";
            public const string ActionColumn = "ui.user_sessions.column.action";
            public const string ThisSessionBadge = "ui.user_sessions.this_session_badge";
            public const string SignOutButton = "ui.user_sessions.sign_out_button";
        }

        public static class Home {
            public const string PageTitle = "ui.home.page_title";
            public const string Heading = "ui.home.heading";
            public const string PendingApplicationsTitle = "ui.home.pending_applications_title";
            public const string PendingApplicationsEmpty = "ui.home.pending_applications_empty";
            public const string SubmittedColumn = "ui.home.submitted_column";
            public const string PendingDueSelectionsTitle = "ui.home.pending_due_selections_title";
            public const string PendingDueSelectionsEmpty = "ui.home.pending_due_selections_empty";
            public const string SelectedFeeColumn = "ui.home.selected_fee_column";
            public const string OpenMotionsTitle = "ui.home.open_motions_title";
            public const string OpenMotionsEmpty = "ui.home.open_motions_empty";
            public const string UpcomingEventsTitle = "ui.home.upcoming_events_title";
            public const string WelcomeText = "ui.home.welcome_text";
        }

        public static class ConfirmSubmission {
            public const string PageTitle = "ui.confirm_submission.page_title";
            public const string Processing = "ui.confirm_submission.processing";
            public const string ConfirmedHeading = "ui.confirm_submission.confirmed_heading";
            public const string ConfirmedBody = "ui.confirm_submission.confirmed_body";
            public const string AlreadyConfirmedHeading = "ui.confirm_submission.already_confirmed_heading";
            public const string AlreadyConfirmedBody = "ui.confirm_submission.already_confirmed_body";
            public const string ExpiredHeading = "ui.confirm_submission.expired_heading";
            public const string ExpiredBody = "ui.confirm_submission.expired_body";
            public const string InvalidHeading = "ui.confirm_submission.invalid_heading";
            public const string InvalidBody = "ui.confirm_submission.invalid_body";
        }

        public static class NotificationPreferences {
            public const string PageTitle = "ui.notification_preferences.page_title";
            public const string Description = "ui.notification_preferences.description";
            public const string TelegramSectionTitle = "ui.notification_preferences.telegram_section_title";
            public const string TelegramLinkedWithChatId = "ui.notification_preferences.telegram_linked_with_chat_id";
            public const string TelegramUnlink = "ui.notification_preferences.telegram_unlink";
            public const string TelegramOpenBot = "ui.notification_preferences.telegram_open_bot";
            public const string TelegramOpenBotFallback = "ui.notification_preferences.telegram_open_bot_fallback";
            public const string TelegramPressStart = "ui.notification_preferences.telegram_press_start";
            public const string TelegramSendCommand = "ui.notification_preferences.telegram_send_command";
            public const string TelegramCopyTooltip = "ui.notification_preferences.telegram_copy_tooltip";
            public const string TelegramCheckLink = "ui.notification_preferences.telegram_check_link";
            public const string TelegramCheckLinkTooltip = "ui.notification_preferences.telegram_check_link_tooltip";
            public const string TelegramValidUntil = "ui.notification_preferences.telegram_valid_until";
            public const string TelegramPrompt = "ui.notification_preferences.telegram_prompt";
            public const string TelegramLinkButton = "ui.notification_preferences.telegram_link_button";
            public const string LoadFailed = "ui.notification_preferences.load_failed";
            public const string TriggerColumn = "ui.notification_preferences.trigger_column";
        }

        public static class PersonalData {
            public const string PageTitle = "ui.personal_data.page_title";
            public const string ProxyApplicationNotice = "ui.personal_data.proxy_application_notice";
            public const string FirstNameLabel = "ui.personal_data.first_name_label";
            public const string LastNameLabel = "ui.personal_data.last_name_label";
            public const string DateOfBirthLabel = "ui.personal_data.date_of_birth_label";
            public const string MinimumAgeHint = "ui.personal_data.minimum_age_hint";
            public const string CitizenshipLabel = "ui.personal_data.citizenship_label";
            public const string EmailLabel = "ui.personal_data.email_label";
            public const string PhoneLabel = "ui.personal_data.phone_label";
        }

        public static class CountrySelection {
            public const string PageTitle = "ui.country_selection.page_title";
            public const string LivesInGermany = "ui.country_selection.lives_in_germany";
            public const string LivesElsewhere = "ui.country_selection.lives_elsewhere";
            public const string GermanyCountryName = "ui.country_selection.germany_country_name";
        }

        public static class MunicipalitySearch {
            public const string PageTitle = "ui.municipality_search.page_title";
            public const string EnterManuallyLink = "ui.municipality_search.enter_manually_link";
        }

        public static class AddressDetails {
            public const string PageTitle = "ui.address_details.page_title";
            public const string CountryLabel = "ui.address_details.country_label";
            public const string PostCodeLabel = "ui.address_details.post_code_label";
            public const string CityLabel = "ui.address_details.city_label";
            public const string FromMunicipalityHint = "ui.address_details.from_municipality_hint";
            public const string StreetLabel = "ui.address_details.street_label";
            public const string HouseNumberLabel = "ui.address_details.house_number_label";
            public const string AssignedChapterTitle = "ui.address_details.assigned_chapter_title";
        }

        public static class DuesTypeSelection {
            public const string PageTitle = "ui.dues_type_selection.page_title";
            public const string Instruction = "ui.dues_type_selection.instruction";
            public const string OnePercentLabel = "ui.dues_type_selection.one_percent_label";
            public const string RecommendedBadge = "ui.dues_type_selection.recommended_badge";
            public const string MonthlyPayLabel = "ui.dues_type_selection.monthly_pay_label";
            public const string UnderageLabel = "ui.dues_type_selection.underage_label";
            public const string UnderageHint = "ui.dues_type_selection.underage_hint";
            public const string ReducedLabel = "ui.dues_type_selection.reduced_label";
        }

        public static class Declarations {
            public const string PageTitle = "ui.declarations.page_title";
            public const string ConformityHeader = "ui.declarations.conformity_header";
            public const string ConformityBody = "ui.declarations.conformity_body";
            public const string NoPriorDeclinedLabel = "ui.declarations.no_prior_declined_label";
            public const string NoPriorDeclinedHint = "ui.declarations.no_prior_declined_hint";
            public const string OtherPartyLabel = "ui.declarations.other_party_label";
            public const string OtherPartyHint = "ui.declarations.other_party_hint";
            public const string EntryDateLabel = "ui.declarations.entry_date_label";
            public const string AdditionalMessageLabel = "ui.declarations.additional_message_label";
        }

        public static class ApplicationSummary {
            public const string PageTitle = "ui.application_summary.page_title";
            public const string DirectSubmittedNotice = "ui.application_summary.direct_submitted_notice";
            public const string PersonalDataSection = "ui.application_summary.personal_data_section";
            public const string NameLabel = "ui.application_summary.name_label";
            public const string DateOfBirthLabel = "ui.application_summary.date_of_birth_label";
            public const string CitizenshipLabel = "ui.application_summary.citizenship_label";
            public const string EmailLabel = "ui.application_summary.email_label";
            public const string PhoneLabel = "ui.application_summary.phone_label";
            public const string AddressSection = "ui.application_summary.address_section";
            public const string ChapterLabel = "ui.application_summary.chapter_label";
            public const string DuesSection = "ui.application_summary.dues_section";
            public const string YearlyDue = "ui.application_summary.yearly_due";
            public const string DueNotSet = "ui.application_summary.due_not_set";
            public const string DeclarationsSection = "ui.application_summary.declarations_section";
            public const string ConformityAccepted = "ui.application_summary.conformity_accepted";
            public const string ConformityNotAccepted = "ui.application_summary.conformity_not_accepted";
            public const string ConformityLine = "ui.application_summary.conformity_line";
            public const string PriorDeclined = "ui.application_summary.prior_declined";
            public const string MemberOfAnotherParty = "ui.application_summary.member_of_another_party";
            public const string PrivacyNotice = "ui.application_summary.privacy_notice";
            public const string ConfirmCorrectHeading = "ui.application_summary.confirm_correct_heading";
            public const string SubmitButton = "ui.application_summary.submit_button";
        }

        public static class ApplicationAdmin {
            public const string PageTitle = "ui.application_admin.page_title";
            public const string ChapterFilterLabel = "ui.application_admin.chapter_filter_label";
            public const string StatusFilterLabel = "ui.application_admin.status_filter_label";
            public const string StatusAll = "ui.application_admin.status_all";
            public const string TotalCount = "ui.application_admin.total_count";
            public const string CityColumn = "ui.application_admin.city_column";
            public const string SubmittedColumn = "ui.application_admin.submitted_column";
        }

        public static class ApplicationStatus {
            public const string Pending = "ui.application_status.pending";
            public const string Approved = "ui.application_status.approved";
            public const string Rejected = "ui.application_status.rejected";
            public const string PendingDivisionLinking = "ui.application_status.pending_division_linking";
        }

        public static class ApplicationDetail {
            public const string NotFound = "ui.application_detail.not_found";
            public const string Heading = "ui.application_detail.heading";
            public const string DivisionLinkingTitle = "ui.application_detail.division_linking_title";
            public const string DivisionLinkingInstruction = "ui.application_detail.division_linking_instruction";
            public const string ProvidedAddressLabel = "ui.application_detail.provided_address_label";
            public const string DivisionSelected = "ui.application_detail.division_selected";
            public const string ChapterPrefix = "ui.application_detail.chapter_prefix";
            public const string NoMatchingChapter = "ui.application_detail.no_matching_chapter";
            public const string LinkAndForward = "ui.application_detail.link_and_forward";
            public const string NoGermanResidence = "ui.application_detail.no_german_residence";
            public const string PersonalDataSection = "ui.application_detail.personal_data_section";
            public const string FirstNameLabel = "ui.application_detail.first_name_label";
            public const string LastNameLabel = "ui.application_detail.last_name_label";
            public const string DateOfBirthLabel = "ui.application_detail.date_of_birth_label";
            public const string CitizenshipLabel = "ui.application_detail.citizenship_label";
            public const string PhoneLabel = "ui.application_detail.phone_label";
            public const string AddressSection = "ui.application_detail.address_section";
            public const string StreetLabel = "ui.application_detail.street_label";
            public const string PostCodeCityLabel = "ui.application_detail.post_code_city_label";
            public const string ChapterLabel = "ui.application_detail.chapter_label";
            public const string DuesSection = "ui.application_detail.dues_section";
            public const string ValuationTypeLabel = "ui.application_detail.valuation_type_label";
            public const string YearlyDueLabel = "ui.application_detail.yearly_due_label";
            public const string ReducedAmountLabel = "ui.application_detail.reduced_amount_label";
            public const string ReducedJustificationLabel = "ui.application_detail.reduced_justification_label";
            public const string DueStatusLabel = "ui.application_detail.due_status_label";
            public const string DeclarationsSection = "ui.application_detail.declarations_section";
            public const string ConformityLabel = "ui.application_detail.conformity_label";
            public const string ConformityAccepted = "ui.application_detail.conformity_accepted";
            public const string ConformityNotAccepted = "ui.application_detail.conformity_not_accepted";
            public const string PriorDeclinedLabel = "ui.application_detail.prior_declined_label";
            public const string OtherPartyLabel = "ui.application_detail.other_party_label";
            public const string MessageHeading = "ui.application_detail.message_heading";
            public const string TimingSection = "ui.application_detail.timing_section";
            public const string EntryDateLabel = "ui.application_detail.entry_date_label";
            public const string SubmittedAtLabel = "ui.application_detail.submitted_at_label";
            public const string ProcessedAtLabel = "ui.application_detail.processed_at_label";
            public const string ActivateMemberSection = "ui.application_detail.activate_member_section";
            public const string WelcomeSentPrefix = "ui.application_detail.welcome_sent_prefix";
            public const string WelcomeSentMemberNumber = "ui.application_detail.welcome_sent_member_number";
            public const string ActivateInstruction = "ui.application_detail.activate_instruction";
            public const string MemberNumberLabel = "ui.application_detail.member_number_label";
            public const string SendWelcomeButton = "ui.application_detail.send_welcome_button";
            public const string LinkedMotionButton = "ui.application_detail.linked_motion_button";
            public const string ValuationMonthlyPay = "ui.application_detail.valuation.monthly_pay";
            public const string ValuationOnePercent = "ui.application_detail.valuation.one_percent";
            public const string ValuationUnderage = "ui.application_detail.valuation.underage";
            public const string ValuationReduced = "ui.application_detail.valuation.reduced";
            public const string ValuationUnknown = "ui.application_detail.valuation.unknown";
            public const string DueStatusPending = "ui.application_detail.due_status.pending";
            public const string DueStatusApproved = "ui.application_detail.due_status.approved";
            public const string DueStatusRejected = "ui.application_detail.due_status.rejected";
            public const string DueStatusAutoApproved = "ui.application_detail.due_status.auto_approved";
            public const string DueStatusUnknown = "ui.application_detail.due_status.unknown";
        }

        public static class DueSelectorUserData {
            public const string PageTitle = "ui.due_selector_user_data.page_title";
            public const string EmailLabel = "ui.due_selector_user_data.email_label";
            public const string MemberNumberLabel = "ui.due_selector_user_data.member_number_label";
            public const string PrefillNotice = "ui.due_selector_user_data.prefill_notice";
        }

        public static class DueTypeSelector {
            public const string PageTitle = "ui.due_type_selector.page_title";
        }

        public static class PaymentOptionSelection {
            public const string PageTitle = "ui.payment_option_selection.page_title";
            public const string DirectDepositInstruction = "ui.payment_option_selection.direct_deposit_instruction";
            public const string DirectDepositMandate = "ui.payment_option_selection.direct_deposit_mandate";
            public const string AccountHolderLabel = "ui.payment_option_selection.account_holder_label";
            public const string UseFullNameLink = "ui.payment_option_selection.use_full_name_link";
            public const string IbanLabel = "ui.payment_option_selection.iban_label";
            public const string PaymentScheduleHeader = "ui.payment_option_selection.payment_schedule_header";
            public const string ScheduleAnnual = "ui.payment_option_selection.schedule.annual";
            public const string ScheduleQuarterly = "ui.payment_option_selection.schedule.quarterly";
            public const string ScheduleMonthly = "ui.payment_option_selection.schedule.monthly";
        }

        public static class SelectByMonthlyPay {
            public const string PageTitle = "ui.select_by_monthly_pay.page_title";
            public const string IncomeGroupLabel = "ui.select_by_monthly_pay.income_group_label";
            public const string CalculatedDueText = "ui.select_by_monthly_pay.calculated_due_text";
        }

        public static class SelectOnePercentYearlyPay {
            public const string PageTitle = "ui.select_one_percent_yearly_pay.page_title";
            public const string YearlyIncomeLabel = "ui.select_one_percent_yearly_pay.yearly_income_label";
            public const string IncomeTooLowLine1 = "ui.select_one_percent_yearly_pay.income_too_low_line1";
            public const string IncomeTooLowLine2 = "ui.select_one_percent_yearly_pay.income_too_low_line2";
            public const string IncomeTooLowLine3 = "ui.select_one_percent_yearly_pay.income_too_low_line3";
            public const string CalculatedYearlyDueLabel = "ui.select_one_percent_yearly_pay.calculated_yearly_due_label";
        }

        public static class SelectReduced {
            public const string PageTitle = "ui.select_reduced.page_title";
            public const string JustificationLabel = "ui.select_reduced.justification_label";
            public const string YearlyAmountLabel = "ui.select_reduced.yearly_amount_label";
            public const string AmountTooLowFeedback = "ui.select_reduced.amount_too_low_feedback";
            public const string TimeSpanOneYear = "ui.select_reduced.time_span.one_year";
            public const string TimeSpanPermanent = "ui.select_reduced.time_span.permanent";
            public const string ClassificationHeader = "ui.select_reduced.classification_header";
            public const string ClassificationNotice = "ui.select_reduced.classification_notice";
            public const string MonthlyClassificationOption = "ui.select_reduced.monthly_classification_option";
            public const string JustificationRequired = "ui.select_reduced.justification_required";
            public const string SkipOption = "ui.select_reduced.skip_option";
        }

        public static class DueSelectorSummary {
            public const string PageTitle = "ui.due_selector_summary.page_title";
            public const string SubmittedDirect = "ui.due_selector_summary.submitted_direct";
            public const string YourChosenDueHeader = "ui.due_selector_summary.your_chosen_due_header";
            public const string MonthlyPayAmountLine = "ui.due_selector_summary.monthly_pay_amount_line";
            public const string MonthlyPayBasisLine = "ui.due_selector_summary.monthly_pay_basis_line";
            public const string OnePercentAmountLine = "ui.due_selector_summary.one_percent_amount_line";
            public const string OnePercentBasisLine = "ui.due_selector_summary.one_percent_basis_line";
            public const string ReducedAmountLine = "ui.due_selector_summary.reduced_amount_line";
            public const string ReducedJustificationLine = "ui.due_selector_summary.reduced_justification_line";
            public const string ReducedPermanentLine = "ui.due_selector_summary.reduced_permanent_line";
            public const string ReducedOneYearLine = "ui.due_selector_summary.reduced_one_year_line";
            public const string ReducedFallbackLine = "ui.due_selector_summary.reduced_fallback_line";
            public const string UnderageLine = "ui.due_selector_summary.underage_line";
            public const string PaymentMethodHeader = "ui.due_selector_summary.payment_method_header";
            public const string DirectDepositLine = "ui.due_selector_summary.direct_deposit_line";
            public const string AccountHolderLabel = "ui.due_selector_summary.account_holder_label";
            public const string IbanLabel = "ui.due_selector_summary.iban_label";
            public const string ManualTransferLine = "ui.due_selector_summary.manual_transfer_line";
            public const string PaymentScheduleHeader = "ui.due_selector_summary.payment_schedule_header";
            public const string AnnualLine = "ui.due_selector_summary.annual_line";
            public const string QuarterlyLineWithAmount = "ui.due_selector_summary.quarterly_line_with_amount";
            public const string QuarterlyLineReduced = "ui.due_selector_summary.quarterly_line_reduced";
            public const string MonthlyLineWithAmount = "ui.due_selector_summary.monthly_line_with_amount";
            public const string MonthlyLineReduced = "ui.due_selector_summary.monthly_line_reduced";
            public const string AreAllDataCorrectHeader = "ui.due_selector_summary.are_all_data_correct_header";
            public const string SubmitFormButton = "ui.due_selector_summary.submit_form_button";
        }

        public static class DueSelectionAdmin {
            public const string PageTitle = "ui.due_selection_admin.page_title";
            public const string StatusAll = "ui.due_selection_admin.status_all";
            public const string StatusPending = "ui.due_selection_admin.status_pending";
            public const string StatusApproved = "ui.due_selection_admin.status_approved";
            public const string StatusRejected = "ui.due_selection_admin.status_rejected";
            public const string StatusAutoApproved = "ui.due_selection_admin.status_auto_approved";
            public const string StatusAutoBadge = "ui.due_selection_admin.status_auto_badge";
            public const string TotalCount = "ui.due_selection_admin.total_count";
            public const string SelectedDueColumn = "ui.due_selection_admin.selected_due_column";
            public const string ReducedAmountColumn = "ui.due_selection_admin.reduced_amount_column";
            public const string JustificationColumn = "ui.due_selection_admin.justification_column";
        }

        public static class DueSelectionDetail {
            public const string NotFound = "ui.due_selection_detail.not_found";
            public const string Heading = "ui.due_selection_detail.heading";
            public const string StatusPending = "ui.due_selection_detail.status_pending";
            public const string StatusApproved = "ui.due_selection_detail.status_approved";
            public const string StatusRejected = "ui.due_selection_detail.status_rejected";
            public const string StatusAutoApproved = "ui.due_selection_detail.status_auto_approved";
            public const string PersonSection = "ui.due_selection_detail.person_section";
            public const string MemberNumberLabel = "ui.due_selection_detail.member_number_label";
            public const string ClassificationSection = "ui.due_selection_detail.classification_section";
            public const string ValuationTypeLabel = "ui.due_selection_detail.valuation_type_label";
            public const string YearlyDueLabel = "ui.due_selection_detail.yearly_due_label";
            public const string YearlyIncomeLabel = "ui.due_selection_detail.yearly_income_label";
            public const string MonthlyIncomeLabel = "ui.due_selection_detail.monthly_income_label";
            public const string ReducedAmountLabel = "ui.due_selection_detail.reduced_amount_label";
            public const string JustificationLabel = "ui.due_selection_detail.justification_label";
            public const string ReducedTimeSpanLabel = "ui.due_selection_detail.reduced_time_span_label";
            public const string ReducedTimeSpanOneYear = "ui.due_selection_detail.reduced_time_span.one_year";
            public const string ReducedTimeSpanPermanent = "ui.due_selection_detail.reduced_time_span.permanent";
            public const string PaymentSection = "ui.due_selection_detail.payment_section";
            public const string PaymentMethodLabel = "ui.due_selection_detail.payment_method_label";
            public const string PaymentMethodDirectDeposit = "ui.due_selection_detail.payment_method.direct_deposit";
            public const string PaymentMethodTransfer = "ui.due_selection_detail.payment_method.transfer";
            public const string AccountHolderLabel = "ui.due_selection_detail.account_holder_label";
            public const string IbanLabel = "ui.due_selection_detail.iban_label";
            public const string PaymentScheduleLabel = "ui.due_selection_detail.payment_schedule_label";
            public const string ProcessingSection = "ui.due_selection_detail.processing_section";
            public const string ProcessedAtPrefix = "ui.due_selection_detail.processed_at_prefix";
            public const string LinkedMotionButton = "ui.due_selection_detail.linked_motion_button";
            public const string ValuationMonthlyPay = "ui.due_selection_detail.valuation.monthly_pay";
            public const string ValuationOnePercent = "ui.due_selection_detail.valuation.one_percent";
            public const string ValuationUnderage = "ui.due_selection_detail.valuation.underage";
            public const string ValuationReduced = "ui.due_selection_detail.valuation.reduced";
            public const string ValuationUnknown = "ui.due_selection_detail.valuation.unknown";
            public const string ScheduleAnnual = "ui.due_selection_detail.schedule.annual";
            public const string ScheduleQuarterly = "ui.due_selection_detail.schedule.quarterly";
            public const string ScheduleMonthly = "ui.due_selection_detail.schedule.monthly";
            public const string ScheduleNone = "ui.due_selection_detail.schedule.none";
        }

        public static class MeetingStatus {
            public const string All = "ui.meeting_status.all";
            public const string Draft = "ui.meeting_status.draft";
            public const string Scheduled = "ui.meeting_status.scheduled";
            public const string InProgress = "ui.meeting_status.in_progress";
            public const string Completed = "ui.meeting_status.completed";
            public const string Archived = "ui.meeting_status.archived";
        }

        public static class MeetingVisibility {
            public const string All = "ui.meeting_visibility.all";
            public const string Public = "ui.meeting_visibility.public";
            public const string Private = "ui.meeting_visibility.private";
        }

        public static class MeetingList {
            public const string PageTitle = "ui.meeting_list.page_title";
            public const string NewMeeting = "ui.meeting_list.new_meeting";
            public const string DateFromLabel = "ui.meeting_list.date_from_label";
            public const string DateToLabel = "ui.meeting_list.date_to_label";
            public const string TotalCount = "ui.meeting_list.total_count";
            public const string EmptyMessage = "ui.meeting_list.empty_message";
            public const string AgendaItemsColumn = "ui.meeting_list.agenda_items_column";
        }

        public static class MeetingDetail {
            public const string NotFound = "ui.meeting_detail.not_found";
            public const string DateTimeLabel = "ui.meeting_detail.date_time_label";
            public const string LocationLabel = "ui.meeting_detail.location_label";
            public const string DescriptionMarkdownLabel = "ui.meeting_detail.description_markdown_label";
            public const string TabAgenda = "ui.meeting_detail.tab_agenda";
            public const string TabProtocol = "ui.meeting_detail.tab_protocol";
            public const string TabAudit = "ui.meeting_detail.tab_audit";
            public const string AgendaCardTitle = "ui.meeting_detail.agenda_card_title";
            public const string ProtocolCardTitle = "ui.meeting_detail.protocol_card_title";
            public const string ProtocolDownloadMarkdown = "ui.meeting_detail.protocol_download_markdown";
            public const string ProtocolDownloadPdf = "ui.meeting_detail.protocol_download_pdf";
            public const string ProtocolUnavailable = "ui.meeting_detail.protocol_unavailable";
            public const string AuditCardTitle = "ui.meeting_detail.audit_card_title";
            public const string AuditTimestampColumn = "ui.meeting_detail.audit_timestamp_column";
            public const string AuditActionColumn = "ui.meeting_detail.audit_action_column";
            public const string AuditFieldColumn = "ui.meeting_detail.audit_field_column";
            public const string AuditOldValueColumn = "ui.meeting_detail.audit_old_value_column";
            public const string AuditNewValueColumn = "ui.meeting_detail.audit_new_value_column";
            public const string AuditUserColumn = "ui.meeting_detail.audit_user_column";
            public const string GoToLiveMeeting = "ui.meeting_detail.go_to_live_meeting";
            public const string DownloadPdfSnapshot = "ui.meeting_detail.download_pdf_snapshot";
            public const string PublicFinalizeConfirm = "ui.meeting_detail.public_finalize_confirm";
            public const string ArchiveConfirm = "ui.meeting_detail.archive_confirm";
            public const string CompleteConfirm = "ui.meeting_detail.complete_confirm";
            public const string StatusChangedToast = "ui.meeting_detail.status_changed_toast";
            public const string TransitionFinalize = "ui.meeting_detail.transition.finalize";
            public const string TransitionBackToDraft = "ui.meeting_detail.transition.back_to_draft";
            public const string TransitionStart = "ui.meeting_detail.transition.start";
            public const string TransitionComplete = "ui.meeting_detail.transition.complete";
            public const string TransitionBackToInProgress = "ui.meeting_detail.transition.back_to_in_progress";
            public const string TransitionArchive = "ui.meeting_detail.transition.archive";
            public const string TransitionUnarchive = "ui.meeting_detail.transition.unarchive";
        }

        public static class MeetingCreate {
            public const string PageTitle = "ui.meeting_create.page_title";
            public const string DateTimeLabel = "ui.meeting_create.date_time_label";
            public const string LocationLabel = "ui.meeting_create.location_label";
            public const string TitlePlaceholder = "ui.meeting_create.title_placeholder";
            public const string LocationPlaceholder = "ui.meeting_create.location_placeholder";
            public const string DescriptionMarkdownLabel = "ui.meeting_create.description_markdown_label";
            public const string Submitting = "ui.meeting_create.submitting";
            public const string SubmitButton = "ui.meeting_create.submit_button";
        }

        public static class MeetingAgendaEdit {
            public const string NotFound = "ui.meeting_agenda_edit.not_found";
            public const string BackToMeeting = "ui.meeting_agenda_edit.back_to_meeting";
            public const string GoToLiveMeeting = "ui.meeting_agenda_edit.go_to_live_meeting";
            public const string PageTitle = "ui.meeting_agenda_edit.page_title";
            public const string AgendaHeading = "ui.meeting_agenda_edit.agenda_heading";
            public const string NewItem = "ui.meeting_agenda_edit.new_item";
            public const string NewItemDefaultTitle = "ui.meeting_agenda_edit.new_item_default_title";
        }

        public static class MeetingLive {
            public const string BackToMeeting = "ui.meeting_live.back_to_meeting";
            public const string EditAgenda = "ui.meeting_live.edit_agenda";
            public const string NotFound = "ui.meeting_live.not_found";
            public const string NotInProgress = "ui.meeting_live.not_in_progress";
            public const string InProgressBadge = "ui.meeting_live.in_progress_badge";
            public const string FinishMeeting = "ui.meeting_live.finish_meeting";
            public const string ProgressHeading = "ui.meeting_live.progress_heading";
            public const string SelectItemHint = "ui.meeting_live.select_item_hint";
            public const string StartNow = "ui.meeting_live.start_now";
            public const string CompleteItem = "ui.meeting_live.complete_item";
            public const string CompletedBadge = "ui.meeting_live.completed_badge";
            public const string ReopenItem = "ui.meeting_live.reopen_item";
            public const string MotionLabel = "ui.meeting_live.motion_label";
            public const string VotingHeading = "ui.meeting_live.voting_heading";
            public const string OfficerRoleColumn = "ui.meeting_live.officer_role_column";
            public const string VoteColumn = "ui.meeting_live.vote_column";
            public const string VoteYes = "ui.meeting_live.vote_yes";
            public const string VoteNo = "ui.meeting_live.vote_no";
            public const string VoteAbstain = "ui.meeting_live.vote_abstain";
            public const string VoteYesCount = "ui.meeting_live.vote_yes_count";
            public const string VoteNoCount = "ui.meeting_live.vote_no_count";
            public const string VoteAbstainCount = "ui.meeting_live.vote_abstain_count";
            public const string CloseVote = "ui.meeting_live.close_vote";
            public const string PresenceHeading = "ui.meeting_live.presence_heading";
            public const string PresentColumn = "ui.meeting_live.present_column";
        }

        public static class AgendaItemEditor {
            public const string EmptyMessage = "ui.agenda_item_editor.empty_message";
            public const string TitlePlaceholder = "ui.agenda_item_editor.title_placeholder";
            public const string SaveAria = "ui.agenda_item_editor.save_aria";
            public const string CancelAria = "ui.agenda_item_editor.cancel_aria";
            public const string ImportMotionsTooltip = "ui.agenda_item_editor.import_motions_tooltip";
            public const string AddChildTooltip = "ui.agenda_item_editor.add_child_tooltip";
            public const string OutdentTooltip = "ui.agenda_item_editor.outdent_tooltip";
            public const string IndentTooltip = "ui.agenda_item_editor.indent_tooltip";
            public const string MoveUpTooltip = "ui.agenda_item_editor.move_up_tooltip";
            public const string MoveDownTooltip = "ui.agenda_item_editor.move_down_tooltip";
            public const string EditTooltip = "ui.agenda_item_editor.edit_tooltip";
            public const string DeleteTooltip = "ui.agenda_item_editor.delete_tooltip";
        }

        public static class AgendaItemType {
            public const string Discussion = "ui.agenda_item_type.discussion";
            public const string Motion = "ui.agenda_item_type.motion";
            public const string Protocol = "ui.agenda_item_type.protocol";
            public const string Break = "ui.agenda_item_type.break";
            public const string Information = "ui.agenda_item_type.information";
            public const string Section = "ui.agenda_item_type.section";
            public const string Presence = "ui.agenda_item_type.presence";
        }

        public static class EventVisibility {
            public const string Public = "ui.event_visibility.public";
            public const string MembersOnly = "ui.event_visibility.members_only";
            public const string Private = "ui.event_visibility.private";
        }

        public static class EventList {
            public const string PageTitle = "ui.event_list.page_title";
            public const string NewEvent = "ui.event_list.new_event";
            public const string FromTemplate = "ui.event_list.from_template";
            public const string IncludeArchived = "ui.event_list.include_archived";
            public const string TotalCount = "ui.event_list.total_count";
            public const string PublicNameColumn = "ui.event_list.public_name_column";
            public const string ProgressColumn = "ui.event_list.progress_column";
        }

        public static class EventDetail {
            public const string NotFound = "ui.event_detail.not_found";
            public const string SaveAria = "ui.event_detail.save_aria";
            public const string CancelAria = "ui.event_detail.cancel_aria";
            public const string EditAria = "ui.event_detail.edit_aria";
            public const string DoneBadge = "ui.event_detail.done_badge";
            public const string CreatedPrefix = "ui.event_detail.created_prefix";
            public const string PublicNameLabel = "ui.event_detail.public_name_label";
            public const string DescriptionMarkdownLabel = "ui.event_detail.description_markdown_label";
            public const string PreviewLabel = "ui.event_detail.preview_label";
            public const string AvailableVariablesHint = "ui.event_detail.available_variables_hint";
            public const string AuditLogTitle = "ui.event_detail.audit_log_title";
            public const string AuditTimestampColumn = "ui.event_detail.audit_timestamp_column";
            public const string AuditActionColumn = "ui.event_detail.audit_action_column";
            public const string AuditFieldColumn = "ui.event_detail.audit_field_column";
            public const string AuditOldValueColumn = "ui.event_detail.audit_old_value_column";
            public const string AuditNewValueColumn = "ui.event_detail.audit_new_value_column";
            public const string AuditUserColumn = "ui.event_detail.audit_user_column";
            public const string SaveAsTemplate = "ui.event_detail.save_as_template";
            public const string TransitionActivate = "ui.event_detail.transition.activate";
            public const string TransitionBackToDraft = "ui.event_detail.transition.back_to_draft";
            public const string TransitionMarkCompleted = "ui.event_detail.transition.mark_completed";
            public const string TransitionBackToActive = "ui.event_detail.transition.back_to_active";
            public const string TransitionArchive = "ui.event_detail.transition.archive";
            public const string TransitionUnarchive = "ui.event_detail.transition.unarchive";
        }

        public static class EventCreate {
            public const string PageTitle = "ui.event_create.page_title";
            public const string InternalNamePlaceholder = "ui.event_create.internal_name_placeholder";
            public const string Creating = "ui.event_create.creating";
            public const string SubmitButton = "ui.event_create.submit_button";
        }

        public static class EventCreateFromTemplate {
            public const string BackToTemplates = "ui.event_create_from_template.back_to_templates";
            public const string TemplateNotFound = "ui.event_create_from_template.template_not_found";
            public const string PageTitle = "ui.event_create_from_template.page_title";
            public const string VariablesHeading = "ui.event_create_from_template.variables_heading";
            public const string PreviewPublicNameLabel = "ui.event_create_from_template.preview_public_name_label";
            public const string Creating = "ui.event_create_from_template.creating";
            public const string SubmitButton = "ui.event_create_from_template.submit_button";
        }

        public static class EventTemplateList {
            public const string PageTitle = "ui.event_template_list.page_title";
            public const string BackToEvents = "ui.event_template_list.back_to_events";
            public const string VariablesColumn = "ui.event_template_list.variables_column";
            public const string ChecklistColumn = "ui.event_template_list.checklist_column";
            public const string CreateFromTemplate = "ui.event_template_list.create_from_template";
            public const string EmptyMessage = "ui.event_template_list.empty_message";
        }

        public static class EventTemplateSave {
            public const string BackToEvent = "ui.event_template_save.back_to_event";
            public const string PageTitle = "ui.event_template_save.page_title";
            public const string EventNotFound = "ui.event_template_save.event_not_found";
            public const string TemplateNameLabel = "ui.event_template_save.template_name_label";
            public const string VariablesHeading = "ui.event_template_save.variables_heading";
            public const string NoVariablesHint = "ui.event_template_save.no_variables_hint";
            public const string VariableColumn = "ui.event_template_save.variable_column";
            public const string LabelColumn = "ui.event_template_save.label_column";
            public const string TypeColumn = "ui.event_template_save.type_column";
            public const string TypeText = "ui.event_template_save.type.text";
            public const string TypeDate = "ui.event_template_save.type.date";
            public const string TypeTime = "ui.event_template_save.type.time";
            public const string TypeNumber = "ui.event_template_save.type.number";
            public const string TypeOptionTemplate = "ui.event_template_save.type.option_template";
            public const string TypeChapter = "ui.event_template_save.type.chapter";
            public const string Saving = "ui.event_template_save.saving";
            public const string SubmitButton = "ui.event_template_save.submit_button";
        }

        public static class EventChecklistEditor {
            public const string Heading = "ui.event_checklist_editor.heading";
            public const string SaveAria = "ui.event_checklist_editor.save_aria";
            public const string CancelAria = "ui.event_checklist_editor.cancel_aria";
            public const string UseDescriptionAsBody = "ui.event_checklist_editor.use_description_as_body";
            public const string MotionTitlePlaceholder = "ui.event_checklist_editor.motion_title_placeholder";
            public const string RecipientLabel = "ui.event_checklist_editor.recipient_label";
            public const string TargetTypeChapter = "ui.event_checklist_editor.target_type.chapter";
            public const string TargetTypeAdminDivision = "ui.event_checklist_editor.target_type.admin_division";
            public const string TargetTypeManualAddresses = "ui.event_checklist_editor.target_type.manual_addresses";
            public const string ManualAddressesPlaceholder = "ui.event_checklist_editor.manual_addresses_placeholder";
            public const string MotionTextLabel = "ui.event_checklist_editor.motion_text_label";
            public const string MotionTextVariablesHint = "ui.event_checklist_editor.motion_text_variables_hint";
            public const string MotionBadge = "ui.event_checklist_editor.motion_badge";
            public const string EmailBadge = "ui.event_checklist_editor.email_badge";
            public const string DescriptionBadge = "ui.event_checklist_editor.description_badge";
            public const string CreateAndCheck = "ui.event_checklist_editor.create_and_check";
            public const string AlreadyDone = "ui.event_checklist_editor.already_done";
            public const string GoToMotion = "ui.event_checklist_editor.go_to_motion";
            public const string SendAndCheck = "ui.event_checklist_editor.send_and_check";
            public const string EmailPreviewAria = "ui.event_checklist_editor.email_preview_aria";
            public const string MoveUp = "ui.event_checklist_editor.move_up";
            public const string MoveDown = "ui.event_checklist_editor.move_down";
            public const string EditTooltip = "ui.event_checklist_editor.edit_tooltip";
            public const string RemoveAria = "ui.event_checklist_editor.remove_aria";
            public const string NewItemPlaceholder = "ui.event_checklist_editor.new_item_placeholder";
            public const string TypeText = "ui.event_checklist_editor.type.text";
            public const string TypeCreateMotion = "ui.event_checklist_editor.type.create_motion";
            public const string TypeSendEmail = "ui.event_checklist_editor.type.send_email";
            public const string AddItemAria = "ui.event_checklist_editor.add_item_aria";
            public const string NoDescriptionFallback = "ui.event_checklist_editor.no_description_fallback";
            public const string NoDateFallback = "ui.event_checklist_editor.no_date_fallback";
            public const string PreviewTemplatePrefix = "ui.event_checklist_editor.preview_template_prefix";
            public const string PreviewSampleData = "ui.event_checklist_editor.preview_sample_data";
            public const string PreviewNoTemplateConfigured = "ui.event_checklist_editor.preview_no_template_configured";
            public const string PreviewUnavailable = "ui.event_checklist_editor.preview_unavailable";
        }

        public static class PublicEventDetail {
            public const string PageTitle = "ui.public_event_detail.page_title";
            public const string NotPubliclyAvailable = "ui.public_event_detail.not_publicly_available";
        }

        public static class PublicEventList {
            public const string PageTitle = "ui.public_event_list.page_title";
            public const string EmptyMessage = "ui.public_event_list.empty_message";
        }

        public static class MemberList {
            public const string PageTitle = "ui.member_list.page_title";
            public const string ImportHistory = "ui.member_list.import_history";
            public const string SearchPlaceholder = "ui.member_list.search_placeholder";
            public const string OrphanedFilterLabel = "ui.member_list.orphaned_filter_label";
            public const string TotalCount = "ui.member_list.total_count";
            public const string MemberNumberColumn = "ui.member_list.member_number_column";
            public const string CityColumn = "ui.member_list.city_column";
            public const string EntryDateColumn = "ui.member_list.entry_date_column";
            public const string StatusExited = "ui.member_list.status_exited";
            public const string StatusPending = "ui.member_list.status_pending";
            public const string StatusActive = "ui.member_list.status_active";
        }

        public static class MemberDetail {
            public const string NotFound = "ui.member_detail.not_found";
            public const string MemberNumberPrefix = "ui.member_detail.member_number_prefix";
            public const string StatusExited = "ui.member_detail.status_exited";
            public const string StatusPending = "ui.member_detail.status_pending";
            public const string StatusActive = "ui.member_detail.status_active";
            public const string PersonalDataSection = "ui.member_detail.personal_data_section";
            public const string DateOfBirthLabel = "ui.member_detail.date_of_birth_label";
            public const string CitizenshipLabel = "ui.member_detail.citizenship_label";
            public const string AddressSection = "ui.member_detail.address_section";
            public const string StreetLabel = "ui.member_detail.street_label";
            public const string PostCodeCityLabel = "ui.member_detail.post_code_city_label";
            public const string CountryLabel = "ui.member_detail.country_label";
            public const string AdminDivisionLabel = "ui.member_detail.admin_division_label";
            public const string OrphanedBadge = "ui.member_detail.orphaned_badge";
            public const string AssignButton = "ui.member_detail.assign_button";
            public const string ChapterSection = "ui.member_detail.chapter_section";
            public const string FederalStateLabel = "ui.member_detail.federal_state_label";
            public const string CountyLabel = "ui.member_detail.county_label";
            public const string MunicipalityLabel = "ui.member_detail.municipality_label";
            public const string MembershipSection = "ui.member_detail.membership_section";
            public const string EntryDateLabel = "ui.member_detail.entry_date_label";
            public const string ExitDateLabel = "ui.member_detail.exit_date_label";
            public const string AdmissionReferenceLabel = "ui.member_detail.admission_reference_label";
            public const string FeeLabel = "ui.member_detail.fee_label";
            public const string ReducedFeeLabel = "ui.member_detail.reduced_fee_label";
            public const string FirstFeeLabel = "ui.member_detail.first_fee_label";
            public const string ReducedFeeEndLabel = "ui.member_detail.reduced_fee_end_label";
            public const string OpenFeesLabel = "ui.member_detail.open_fees_label";
            public const string PreferencesSection = "ui.member_detail.preferences_section";
            public const string VotingRightsLabel = "ui.member_detail.voting_rights_label";
            public const string SurveysLabel = "ui.member_detail.surveys_label";
            public const string ActionsLabel = "ui.member_detail.actions_label";
            public const string NewsletterLabel = "ui.member_detail.newsletter_label";
            public const string PostBounceLabel = "ui.member_detail.post_bounce_label";
            public const string SystemSection = "ui.member_detail.system_section";
            public const string LinkedUserLabel = "ui.member_detail.linked_user_label";
            public const string NotLinked = "ui.member_detail.not_linked";
            public const string LastImportLabel = "ui.member_detail.last_import_label";
            public const string AuditLogSection = "ui.member_detail.audit_log_section";
            public const string AuditTimestampColumn = "ui.member_detail.audit_timestamp_column";
            public const string AuditActionColumn = "ui.member_detail.audit_action_column";
            public const string AuditFieldColumn = "ui.member_detail.audit_field_column";
            public const string AuditOldValueColumn = "ui.member_detail.audit_old_value_column";
            public const string AuditNewValueColumn = "ui.member_detail.audit_new_value_column";
            public const string AuditUserColumn = "ui.member_detail.audit_user_column";
        }

        public static class MemberImportHistory {
            public const string BackToMembers = "ui.member_import_history.back_to_members";
            public const string PageTitle = "ui.member_import_history.page_title";
            public const string Importing = "ui.member_import_history.importing";
            public const string ManualImport = "ui.member_import_history.manual_import";
            public const string UploadCardTitle = "ui.member_import_history.upload_card_title";
            public const string ChooseCsvFile = "ui.member_import_history.choose_csv_file";
            public const string UploadAndImport = "ui.member_import_history.upload_and_import";
            public const string ImportFinished = "ui.member_import_history.import_finished";
            public const string NoImportsYet = "ui.member_import_history.no_imports_yet";
            public const string TimestampColumn = "ui.member_import_history.timestamp_column";
            public const string FileColumn = "ui.member_import_history.file_column";
            public const string DurationColumn = "ui.member_import_history.duration_column";
            public const string TotalColumn = "ui.member_import_history.total_column";
            public const string NewColumn = "ui.member_import_history.new_column";
            public const string UpdatedColumn = "ui.member_import_history.updated_column";
            public const string ErrorsColumn = "ui.member_import_history.errors_column";
            public const string FileTooLarge = "ui.member_import_history.file_too_large";
        }

        public static class ImportStatus {
            public const string PageTitle = "ui.import_status.page_title";
            public const string MemberImportSection = "ui.import_status.member_import_section";
            public const string UploadCsvButton = "ui.import_status.upload_csv_button";
            public const string LastImport = "ui.import_status.last_import";
            public const string FileLabel = "ui.import_status.file_label";
            public const string DurationLabel = "ui.import_status.duration_label";
            public const string TotalLabel = "ui.import_status.total_label";
            public const string NewLabel = "ui.import_status.new_label";
            public const string UpdatedLabel = "ui.import_status.updated_label";
            public const string ErrorsLabel = "ui.import_status.errors_label";
            public const string HistorySummary = "ui.import_status.history_summary";
            public const string NoImportsYet = "ui.import_status.no_imports_yet";
            public const string AdminDivImportSection = "ui.import_status.admin_div_import_section";
            public const string AddedLabel = "ui.import_status.added_label";
            public const string RemovedLabel = "ui.import_status.removed_label";
            public const string RemappedLabel = "ui.import_status.remapped_label";
            public const string OrphanedLabel = "ui.import_status.orphaned_label";
        }

        public static class ChapterList {
            public const string PageTitle = "ui.chapter_list.page_title";
            public const string NewChapter = "ui.chapter_list.new_chapter";
            public const string TreeViewLink = "ui.chapter_list.tree_view_link";
            public const string SearchPlaceholder = "ui.chapter_list.search_placeholder";
            public const string TotalCount = "ui.chapter_list.total_count";
            public const string ShortCodeColumn = "ui.chapter_list.short_code_column";
            public const string ExternalCodeColumn = "ui.chapter_list.external_code_column";
        }

        public static class ChapterDetail {
            public const string NotFound = "ui.chapter_detail.not_found";
            public const string DeleteDisabledHasChildren = "ui.chapter_detail.delete_disabled_has_children";
            public const string DeleteTooltip = "ui.chapter_detail.delete_tooltip";
            public const string ShortCodeLabel = "ui.chapter_detail.short_code_label";
            public const string ExternalCodeLabel = "ui.chapter_detail.external_code_label";
            public const string ParentChapterLabel = "ui.chapter_detail.parent_chapter_label";
            public const string ParentEmptyLabel = "ui.chapter_detail.parent_empty_label";
            public const string AdminDivisionLabel = "ui.chapter_detail.admin_division_label";
            public const string AdminDivisionHint = "ui.chapter_detail.admin_division_hint";
            public const string BoardSection = "ui.chapter_detail.board_section";
            public const string ChildrenSection = "ui.chapter_detail.children_section";
            public const string NoBoardMembers = "ui.chapter_detail.no_board_members";
            public const string DeleteDialogTitle = "ui.chapter_detail.delete_dialog_title";
            public const string DeleteDialogConfirm = "ui.chapter_detail.delete_dialog_confirm";
            public const string DeleteDialogMessage = "ui.chapter_detail.delete_dialog_message";
        }

        public static class ChapterCreate {
            public const string PageTitle = "ui.chapter_create.page_title";
            public const string ShortCodeLabel = "ui.chapter_create.short_code_label";
            public const string ExternalCodeLabel = "ui.chapter_create.external_code_label";
            public const string ParentLabel = "ui.chapter_create.parent_label";
            public const string ParentEmptyLabel = "ui.chapter_create.parent_empty_label";
            public const string ParentHint = "ui.chapter_create.parent_hint";
            public const string AdminDivisionLabel = "ui.chapter_create.admin_division_label";
            public const string AdminDivisionHint = "ui.chapter_create.admin_division_hint";
            public const string SubmitButton = "ui.chapter_create.submit_button";
        }

        public static class ChapterTree {
            public const string PageTitle = "ui.chapter_tree.page_title";
            public const string NewChapter = "ui.chapter_tree.new_chapter";
            public const string ListViewLink = "ui.chapter_tree.list_view_link";
        }

        public static class ChapterOfficerAdd {
            public const string BackToChapter = "ui.chapter_officer_add.back_to_chapter";
            public const string PageTitle = "ui.chapter_officer_add.page_title";
            public const string SearchSection = "ui.chapter_officer_add.search_section";
            public const string SearchPlaceholder = "ui.chapter_officer_add.search_placeholder";
            public const string NoMembersFound = "ui.chapter_officer_add.no_members_found";
            public const string MemberNumberColumn = "ui.chapter_officer_add.member_number_column";
            public const string CityColumn = "ui.chapter_officer_add.city_column";
            public const string SelectButton = "ui.chapter_officer_add.select_button";
            public const string SelectedMemberSection = "ui.chapter_officer_add.selected_member_section";
            public const string MemberNumberPrefix = "ui.chapter_officer_add.member_number_prefix";
            public const string RoleLabel = "ui.chapter_officer_add.role_label";
            public const string Adding = "ui.chapter_officer_add.adding";
        }

        public static class ChapterOfficerList {
            public const string PageTitle = "ui.chapter_officer_list.page_title";
            public const string SearchPlaceholder = "ui.chapter_officer_list.search_placeholder";
            public const string TotalCount = "ui.chapter_officer_list.total_count";
        }

        public static class RoleList {
            public const string PageTitle = "ui.role_list.page_title";
            public const string AssignmentsLink = "ui.role_list.assignments_link";
            public const string NewRole = "ui.role_list.new_role";
            public const string ScopeColumn = "ui.role_list.scope_column";
            public const string PermissionsColumn = "ui.role_list.permissions_column";
            public const string SystemColumn = "ui.role_list.system_column";
            public const string ScopeGlobal = "ui.role_list.scope_global";
            public const string ScopeChapter = "ui.role_list.scope_chapter";
            public const string ScopeChapterFull = "ui.role_list.scope_chapter_full";
            public const string SystemBadge = "ui.role_list.system_badge";
            public const string DeleteAriaLabel = "ui.role_list.delete_aria_label";
            public const string CreateFormTitle = "ui.role_list.create_form_title";
            public const string DescriptionLabel = "ui.role_list.description_label";
            public const string ScopeLabel = "ui.role_list.scope_label";
            public const string PermissionsLabel = "ui.role_list.permissions_label";
        }

        public static class RoleEdit {
            public const string NotFound = "ui.role_edit.not_found";
            public const string BackLink = "ui.role_edit.back_link";
            public const string ScopeGlobal = "ui.role_edit.scope_global";
            public const string ScopeChapterFull = "ui.role_edit.scope_chapter_full";
            public const string SystemBadge = "ui.role_edit.system_badge";
            public const string SystemNotice = "ui.role_edit.system_notice";
            public const string DescriptionLabel = "ui.role_edit.description_label";
            public const string PermissionsLabel = "ui.role_edit.permissions_label";
        }

        public static class RoleAssignments {
            public const string BackToRoles = "ui.role_assignments.back_to_roles";
            public const string PageTitle = "ui.role_assignments.page_title";
            public const string NewAssignment = "ui.role_assignments.new_assignment";
            public const string NewAssignmentTitle = "ui.role_assignments.new_assignment_title";
            public const string UserLabel = "ui.role_assignments.user_label";
            public const string UserEmptyOption = "ui.role_assignments.user_empty_option";
            public const string RoleLabel = "ui.role_assignments.role_label";
            public const string RoleEmptyOption = "ui.role_assignments.role_empty_option";
            public const string ScopeGlobal = "ui.role_assignments.scope_global";
            public const string ScopeChapter = "ui.role_assignments.scope_chapter";
            public const string ChapterLabel = "ui.role_assignments.chapter_label";
            public const string AssignButton = "ui.role_assignments.assign_button";
            public const string EmptyMessage = "ui.role_assignments.empty_message";
            public const string UserColumn = "ui.role_assignments.user_column";
            public const string RoleColumn = "ui.role_assignments.role_column";
            public const string ChapterColumn = "ui.role_assignments.chapter_column";
            public const string GlobalBadge = "ui.role_assignments.global_badge";
            public const string RemoveButton = "ui.role_assignments.remove_button";
        }

        public static class UserList {
            public const string PageTitle = "ui.user_list.page_title";
            public const string TotalCount = "ui.user_list.total_count";
            public const string UsernameColumn = "ui.user_list.username_column";
        }

        public static class UserDetail {
            public const string NotFound = "ui.user_detail.not_found";
            public const string DeleteButton = "ui.user_detail.delete_button";
            public const string UserDataSection = "ui.user_detail.user_data_section";
            public const string UsernameLabel = "ui.user_detail.username_label";
            public const string GlobalPermissionsSection = "ui.user_detail.global_permissions_section";
            public const string NoGlobalPermissions = "ui.user_detail.no_global_permissions";
            public const string ChapterPermissionsSection = "ui.user_detail.chapter_permissions_section";
            public const string NoChapterPermissions = "ui.user_detail.no_chapter_permissions";
            public const string AddChapterPermissionTitle = "ui.user_detail.add_chapter_permission_title";
            public const string ChapterLabel = "ui.user_detail.chapter_label";
            public const string PermissionLabel = "ui.user_detail.permission_label";
            public const string PermissionEmptyOption = "ui.user_detail.permission_empty_option";
        }

        public static class AdminDivisionSearch {
            public const string PageTitle = "ui.admin_division_search.page_title";
            public const string SearchPlaceholder = "ui.admin_division_search.search_placeholder";
            public const string TotalCount = "ui.admin_division_search.total_count";
            public const string LevelColumn = "ui.admin_division_search.level_column";
            public const string AdminCodeColumn = "ui.admin_division_search.admin_code_column";
            public const string PostCodesColumn = "ui.admin_division_search.post_codes_column";
            public const string DepthWorld = "ui.admin_division_search.depth_world";
            public const string DepthCountry = "ui.admin_division_search.depth_country";
            public const string DepthFederalState = "ui.admin_division_search.depth_federal_state";
            public const string DepthGovernmentRegion = "ui.admin_division_search.depth_government_region";
            public const string DepthDistrict = "ui.admin_division_search.depth_district";
            public const string DepthMunicipality = "ui.admin_division_search.depth_municipality";
            public const string DepthLocality = "ui.admin_division_search.depth_locality";
        }

        public static class AdminDivisionTree {
            public const string PageTitle = "ui.admin_division_tree.page_title";
            public const string PostCodesPrefix = "ui.admin_division_tree.post_codes_prefix";
        }

        public static class SmtpSetup {
            public const string PageTitle = "ui.smtp_setup.page_title";
            public const string ServerSettingsTitle = "ui.smtp_setup.server_settings_title";
            public const string TestSectionTitle = "ui.smtp_setup.test_section_title";
            public const string TestDescription = "ui.smtp_setup.test_description";
            public const string RecipientPlaceholder = "ui.smtp_setup.recipient_placeholder";
            public const string SendTestButton = "ui.smtp_setup.send_test_button";
            public const string TestSuccess = "ui.smtp_setup.test_success";
            public const string TestErrorPrefix = "ui.smtp_setup.test_error_prefix";
        }

        public static class SamlSetup {
            public const string PageTitle = "ui.saml_setup.page_title";
            public const string SettingsTitle = "ui.saml_setup.settings_title";
        }

        public static class OidcSetup {
            public const string PageTitle = "ui.oidc_setup.page_title";
            public const string SettingsTitle = "ui.oidc_setup.settings_title";
        }

        public static class TemplateFieldPalette {
            public const string Heading = "ui.template_field_palette.heading";
            public const string InsertTooltip = "ui.template_field_palette.insert_tooltip";
            public const string ModelGlobals = "ui.template_field_palette.model_globals";
            public const string ModelConfirmation = "ui.template_field_palette.model_confirmation";
            public const string ModelChapter = "ui.template_field_palette.model_chapter";
            public const string ModelApplication = "ui.template_field_palette.model_application";
            public const string ModelSelection = "ui.template_field_palette.model_selection";
            public const string ModelMotion = "ui.template_field_palette.model_motion";
            public const string ModelMember = "ui.template_field_palette.model_member";
            public const string ModelEvent = "ui.template_field_palette.model_event";
        }

        public static class TemplateList {
            public const string PageTitle = "ui.template_list.page_title";
            public const string TypeColumn = "ui.template_list.type_column";
            public const string IdentifierColumn = "ui.template_list.identifier_column";
            public const string TypeSystem = "ui.template_list.type_system";
            public const string TypeChapter = "ui.template_list.type_chapter";
            public const string TypeSystemWide = "ui.template_list.type_system_wide";
            public const string CreateButton = "ui.template_list.create_button";
            public const string Cancel = "ui.template_list.cancel";
        }

        public static class TemplateCreate {
            public const string PageTitle = "ui.template_create.page_title";
            public const string DisplayNameLabel = "ui.template_create.display_name_label";
            public const string TypeLabel = "ui.template_create.type_label";
            public const string TypeSystemWide = "ui.template_create.type_system_wide";
            public const string TypeSystemWideHint = "ui.template_create.type_system_wide_hint";
            public const string TypeChapter = "ui.template_create.type_chapter";
            public const string ChapterPickerLabel = "ui.template_create.chapter_picker_label";
            public const string CreateConfirm = "ui.template_create.create_confirm";
            public const string Cancel = "ui.template_create.cancel";
        }

        public static class TemplateDetail {
            public const string NotFound = "ui.template_detail.not_found";
            public const string SystemBadge = "ui.template_detail.system_badge";
            public const string CustomBadge = "ui.template_detail.custom_badge";
            public const string DisplayNameLabel = "ui.template_detail.display_name_label";
            public const string SubjectLabel = "ui.template_detail.subject_label";
            public const string NoSubjectLabel = "ui.template_detail.no_subject_label";
            public const string BodyLabel = "ui.template_detail.body_label";
            public const string AllowsMemberFieldsLabel = "ui.template_detail.allows_member_fields_label";
            public const string AllowsEventFieldsLabel = "ui.template_detail.allows_event_fields_label";
            public const string AllowsChapterFieldsLabel = "ui.template_detail.allows_chapter_fields_label";
            public const string AllowedFieldsHeading = "ui.template_detail.allowed_fields_heading";
            public const string PdfPreviewButton = "ui.template_detail.pdf_preview_button";
            public const string SaveButton = "ui.template_detail.save_button";
            public const string DeleteButton = "ui.template_detail.delete_button";
            public const string DeleteConfirm = "ui.template_detail.delete_confirm";
            public const string OverridesHeading = "ui.template_detail.overrides_heading";
            public const string NoOverrides = "ui.template_detail.no_overrides";
            public const string AddOverrideHeading = "ui.template_detail.add_override_heading";
            public const string AddOverrideButton = "ui.template_detail.add_override_button";
            public const string ChapterColumn = "ui.template_detail.chapter_column";
            public const string SubjectColumn = "ui.template_detail.subject_column";
            public const string EditButton = "ui.template_detail.edit_button";
        }

        public static class OptionList {
            public const string PageTitle = "ui.option_list.page_title";
            public const string SetupSmtp = "ui.option_list.setup_smtp";
            public const string SetupSaml = "ui.option_list.setup_saml";
            public const string SetupOidc = "ui.option_list.setup_oidc";
            public const string IdentifierColumn = "ui.option_list.identifier_column";
            public const string TypeColumn = "ui.option_list.type_column";
            public const string OverridableColumn = "ui.option_list.overridable_column";
            public const string OverridesColumn = "ui.option_list.overrides_column";
            public const string DataTypeString = "ui.option_list.data_type.string";
            public const string DataTypeNumber = "ui.option_list.data_type.number";
            public const string DataTypeUnknown = "ui.option_list.data_type.unknown";
        }

        public static class OptionDetail {
            public const string NotFound = "ui.option_detail.not_found";
            public const string GlobalValueTitle = "ui.option_detail.global_value_title";
            public const string PreviewButton = "ui.option_detail.preview_button";
            public const string ChapterOverridesTitle = "ui.option_detail.chapter_overrides_title";
            public const string ShortCodeColumn = "ui.option_detail.short_code_column";
            public const string ValueColumn = "ui.option_detail.value_column";
            public const string NoOverrides = "ui.option_detail.no_overrides";
            public const string EditOverrideHeader = "ui.option_detail.edit_override_header";
            public const string AddChapterOption = "ui.option_detail.add_chapter_option";
            public const string AddButton = "ui.option_detail.add_button";
            public const string NotOverridableNotice = "ui.option_detail.not_overridable_notice";
            public const string SaveAriaLabel = "ui.option_detail.save_aria_label";
        }

        public static class LoginLockouts {
            public const string PageTitle = "ui.login_lockouts.page_title";
            public const string RefreshButton = "ui.login_lockouts.refresh_button";
            public const string NoneLocked = "ui.login_lockouts.none_locked";
            public const string TotalCount = "ui.login_lockouts.total_count";
            public const string IpAddressColumn = "ui.login_lockouts.ip_address_column";
            public const string UserColumn = "ui.login_lockouts.user_column";
            public const string FailedAttemptsColumn = "ui.login_lockouts.failed_attempts_column";
            public const string LastAttemptColumn = "ui.login_lockouts.last_attempt_column";
            public const string LockedUntilColumn = "ui.login_lockouts.locked_until_column";
            public const string UnlockButton = "ui.login_lockouts.unlock_button";
        }

        public static class AppErrorBoundary {
            public const string Heading = "ui.app_error_boundary.heading";
            public const string Description = "ui.app_error_boundary.description";
            public const string RecoverButton = "ui.app_error_boundary.recover_button";
        }

        public static class DashboardCard {
            public const string ShowAll = "ui.dashboard_card.show_all";
        }

        public static class DeleteButton {
            public const string DefaultText = "ui.delete_button.default_text";
        }

        public static class FormSaveButton {
            public const string DefaultText = "ui.form_save_button.default_text";
            public const string DefaultSavingText = "ui.form_save_button.default_saving_text";
        }

        public static class MunicipalityPicker {
            public const string NameLabel = "ui.municipality_picker.name_label";
            public const string NamePlaceholder = "ui.municipality_picker.name_placeholder";
            public const string PostCodeLabel = "ui.municipality_picker.post_code_label";
            public const string PostCodePlaceholder = "ui.municipality_picker.post_code_placeholder";
            public const string MunicipalityLabel = "ui.municipality_picker.municipality_label";
            public const string NoResults = "ui.municipality_picker.no_results";
        }

        public static class AdminDivisionPicker {
            public const string Placeholder = "ui.admin_division_picker.placeholder";
            public const string ClearAriaLabel = "ui.admin_division_picker.clear_aria_label";
        }

        public static class ChapterPicker {
            public const string EmptyLabel = "ui.chapter_picker.empty_label";
            public const string InlinePlaceholder = "ui.chapter_picker.inline_placeholder";
            public const string SearchPlaceholder = "ui.chapter_picker.search_placeholder";
            public const string ClearAriaLabel = "ui.chapter_picker.clear_aria_label";
            public const string MoreResults = "ui.chapter_picker.more_results";
        }

        public static class MotionPicker {
            public const string Placeholder = "ui.motion_picker.placeholder";
            public const string ClearAriaLabel = "ui.motion_picker.clear_aria_label";
            public const string LoadingHint = "ui.motion_picker.loading_hint";
            public const string NoOpenMotions = "ui.motion_picker.no_open_motions";
            public const string CreateNewMotion = "ui.motion_picker.create_new_motion";
        }

        public static class TemplatePicker {
            public const string Placeholder = "ui.template_picker.placeholder";
        }

        public static class Pagination {
            public const string PreviousAriaLabel = "ui.pagination.previous_aria_label";
            public const string NextAriaLabel = "ui.pagination.next_aria_label";
        }

        public static class OptionGroupEditor {
            public const string NoMatchingOptions = "ui.option_group_editor.no_matching_options";
            public const string ActivatedLabel = "ui.option_group_editor.activated_label";
            public const string SecretUnchangedPlaceholder = "ui.option_group_editor.secret_unchanged_placeholder";
        }

        public static class SubmissionConfirmationNotice {
            public const string Heading = "ui.submission_confirmation_notice.heading";
            public const string Line1 = "ui.submission_confirmation_notice.line1";
            public const string Line2 = "ui.submission_confirmation_notice.line2";
        }

        public static class Toaster {
            public const string DetailsSummary = "ui.toaster.details_summary";
        }

        public static class MarkdownEditorWithPreview {
            public const string MarkdownLabel = "ui.markdown_editor_with_preview.markdown_label";
            public const string PreviewLabel = "ui.markdown_editor_with_preview.preview_label";
        }

        public static class CodeMirrorEditorWithPreview {
            public const string ReadOnlyNotice = "ui.code_mirror_editor_with_preview.read_only_notice";
            public const string ReconnectingNotice = "ui.code_mirror_editor_with_preview.reconnecting_notice";
            public const string MarkdownLabel = "ui.code_mirror_editor_with_preview.markdown_label";
            public const string PreviewLabel = "ui.code_mirror_editor_with_preview.preview_label";
            public const string SavingState = "ui.code_mirror_editor_with_preview.saving_state";
            public const string DirtyState = "ui.code_mirror_editor_with_preview.dirty_state";
            public const string FailedState = "ui.code_mirror_editor_with_preview.failed_state";
            public const string SavedAtPrefix = "ui.code_mirror_editor_with_preview.saved_at_prefix";
            public const string ActiveEditorsTooltip = "ui.code_mirror_editor_with_preview.active_editors_tooltip";
        }

        public static class LanguageSwitcher {
            public const string Tooltip = "ui.language_switcher.tooltip";
        }
    }
}
