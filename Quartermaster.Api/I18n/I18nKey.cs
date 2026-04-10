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
            public static class Officer {
                public const string MemberRequired = "error.chapter.officer.member_required";
                public const string ChapterRequired = "error.chapter.officer.chapter_required";
                public const string InvalidOfficerType = "error.chapter.officer.invalid_officer_type";
            }
        }

        public static class Motion {
            public const string ChapterRequired = "error.motion.chapter_required";
            public const string SubmitterNameRequired = "error.motion.submitter_name_required";
            public const string SubmitterNameMaxLength = "error.motion.submitter_name_max_length";
            public const string EmailRequired = "error.motion.email_required";
            public const string EmailInvalid = "error.motion.email_invalid";
            public const string EmailMaxLength = "error.motion.email_max_length";
            public const string TitleRequired = "error.motion.title_required";
            public const string TitleMaxLength = "error.motion.title_max_length";
            public const string BodyRequired = "error.motion.body_required";
            public const string BodyMaxLength = "error.motion.body_max_length";

            public static class Vote {
                public const string MotionIdRequired = "error.motion.vote.motion_id_required";
                public const string UserIdRequired = "error.motion.vote.user_id_required";
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
}
