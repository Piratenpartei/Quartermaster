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
    }
}
