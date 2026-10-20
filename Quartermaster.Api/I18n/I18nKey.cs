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
        public static class TravelExpense {
            public const string InputInvalid = "error.travel_expense.input_invalid";
            public const string RulesInvalid = "error.travel_expense.rules_invalid";
            public const string RulesVersionLabelRequired = "error.travel_expense.rules_version_label_required";
            public const string RulesRateStepsInvalid = "error.travel_expense.rules_rate_steps_invalid";
            public const string RulesDuplicateValidFrom = "error.travel_expense.rules_duplicate_valid_from";
            public const string RulesLastGlobalVersion = "error.travel_expense.rules_last_global_version";
            public const string RulesChapterChanged = "error.travel_expense.rules_chapter_changed";
        }

        public static class KeycloakAccount {
            public const string QueryRequired = "error.keycloak_account.query_required";
            public const string QueryTooShort = "error.keycloak_account.query_too_short";
            public const string InvalidMemberNumber = "error.keycloak_account.invalid_member_number";
            public const string NotFound = "error.keycloak_account.not_found";
            public const string NoMemberNumber = "error.keycloak_account.no_member_number";
            public const string NoEmail = "error.keycloak_account.no_email";
            public const string Closed = "error.keycloak_account.closed";
            public const string MemberExited = "error.keycloak_account.member_exited";
            public const string UserExists = "error.keycloak_account.user_exists";
            public const string UserDeleted = "error.keycloak_account.user_deleted";
        }

        public static class Receipt {
            public const string NoFile = "error.receipt.no_file";
            public const string TooLarge = "error.receipt.too_large";
            public const string LimitReached = "error.receipt.limit_reached";
            public const string UnsupportedType = "error.receipt.unsupported_type";
        }

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
            public const string KeycloakDepartmentNotUnique = "error.chapter.keycloak_department_not_unique";
            public const string KeycloakDepartmentTooLong = "error.chapter.keycloak_department_too_long";
            public const string HasChildren = "error.chapter.has_children";
            public const string Archived = "error.chapter.archived";

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

        public static class PublicForm {
            public const string Disabled = "error.public_form.disabled";
            public const string PdfRateLimited = "error.public_form.pdf_rate_limited";
        }

        public static class Motion {
            public const string ChapterRequired = "error.motion.chapter_required";
            public const string ChapterNotFound = "error.motion.chapter_not_found";
            public const string PublicFormDisabled = "error.motion.public_form_disabled";
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

        public static class AssemblyMotion {
            public const string MarkdownHtmlNotAllowed = "error.assembly_motion.markdown_html_not_allowed";
            public const string MarkdownImageNotAllowed = "error.assembly_motion.markdown_image_not_allowed";
            public const string MarkdownTooDeeplyNested = "error.assembly_motion.markdown_too_deeply_nested";
            public const string MarkdownTooLong = "error.assembly_motion.markdown_too_long";
            public const string MarkdownTableTooLarge = "error.assembly_motion.markdown_table_too_large";
            public const string MarkdownTooManyBrackets = "error.assembly_motion.markdown_too_many_brackets";
            public const string MarkdownTooMuchInlineMarkup = "error.assembly_motion.markdown_too_much_inline_markup";
            public const string MarkdownOutputTooLarge = "error.assembly_motion.markdown_output_too_large";
            public const string MarkdownTooManyLines = "error.assembly_motion.markdown_too_many_lines";
            public const string NotFound = "error.assembly_motion.not_found";
            public const string NotAuthor = "error.assembly_motion.not_author";
            public const string NotCommission = "error.assembly_motion.not_commission";
            public const string PortalNotOpen = "error.assembly_motion.portal_not_open";
            public const string PortalClosed = "error.assembly_motion.portal_closed";
            public const string DraftLimitReached = "error.assembly_motion.draft_limit_reached";
            public const string DeadlinePassed = "error.assembly_motion.deadline_passed";
            public const string InvalidState = "error.assembly_motion.invalid_state";
            public const string EditLocked = "error.assembly_motion.edit_locked";
            public const string WikiPrefixInvalid = "error.assembly_motion.wiki_prefix_invalid";
            public const string WikiLoginFailed = "error.assembly_motion.wiki_login_failed";
            public const string WikiNotConfigured = "error.assembly_motion.wiki_not_configured";
            public const string WikiMirrorDisabled = "error.assembly_motion.wiki_mirror_disabled";
            public const string WikiMotionNotNumbered = "error.assembly_motion.wiki_motion_not_numbered";
            public const string OpenSlidesNotConfigured = "error.assembly_motion.openslides_not_configured";
            public const string OpenSlidesMeetingMissing = "error.assembly_motion.openslides_meeting_missing";
            public const string OpenSlidesPushRunning = "error.assembly_motion.openslides_push_running";
            public const string RevisionConflict = "error.assembly_motion.revision_conflict";
            public const string AlreadySubmitter = "error.assembly_motion.already_submitter";
            public const string NotSubmitter = "error.assembly_motion.not_submitter";
            public const string AuthorCannotLeave = "error.assembly_motion.author_cannot_leave";
            public const string TypeNotInPortal = "error.assembly_motion.type_not_in_portal";
            public const string NotEligible = "error.assembly_motion.not_eligible";
            public const string TitleRequired = "error.assembly_motion.title_required";
            public const string TitleMaxLength = "error.assembly_motion.title_max_length";
            public const string SectionMaxLength = "error.assembly_motion.section_max_length";
            public const string TextRequired = "error.assembly_motion.text_required";
            public const string ReasonRequired = "error.assembly_motion.reason_required";
            public const string SummaryMaxLength = "error.assembly_motion.summary_max_length";
            public const string KeywordsMaxLength = "error.assembly_motion.keywords_max_length";
            public const string PriorDiscussionMaxLength = "error.assembly_motion.prior_discussion_max_length";
            public const string CheckNoteRequired = "error.assembly_motion.check_note_required";
            public const string ChangeNoteRequired = "error.assembly_motion.change_note_required";
            public const string CheckNoteMaxLength = "error.assembly_motion.check_note_max_length";
            public const string ChangeNoteMaxLength = "error.assembly_motion.change_note_max_length";
            public const string InquiryDueTooSoon = "error.assembly_motion.inquiry_due_too_soon";
            public const string InquiryQuestionRequired = "error.assembly_motion.inquiry_question_required";
            public const string InquiryQuestionMaxLength = "error.assembly_motion.inquiry_question_max_length";
            public const string InquiryAnswerRequired = "error.assembly_motion.inquiry_answer_required";
            public const string InquiryAnswerMaxLength = "error.assembly_motion.inquiry_answer_max_length";
            public const string InquiryNotFound = "error.assembly_motion.inquiry_not_found";
            public const string CompetitionExists = "error.assembly_motion.competition_exists";
            public const string CompetitionNotFound = "error.assembly_motion.competition_not_found";
            public const string CompetingFlagRequiresLink = "error.assembly_motion.competing_flag_requires_link";
            public const string PrioritizationNotOpen = "error.assembly_motion.prioritization_not_open";
            public const string RankingNotAvailable = "error.assembly_motion.ranking_not_available";
            public const string EventNotReached = "error.assembly_motion.event_not_reached";
            public const string ResultItemsInvalid = "error.assembly_motion.result_items_invalid";
            public const string ResultItemInvalid = "error.assembly_motion.result_item_invalid";

            public static class Portal {
                public const string NotFound = "error.assembly_motion.portal.not_found";
                public const string EventNotFound = "error.assembly_motion.portal.event_not_found";
                public const string AlreadyExists = "error.assembly_motion.portal.already_exists";
                public const string EventRequired = "error.assembly_motion.portal.event_required";
                public const string ClosesBeforeOpens = "error.assembly_motion.portal.closes_before_opens";
                public const string PrioritizationWindowRequired = "error.assembly_motion.portal.prioritization_window_required";
                public const string PrioritizationWindowInvalid = "error.assembly_motion.portal.prioritization_window_invalid";
                public const string PrioritizationEndAfterClose = "error.assembly_motion.portal.prioritization_end_after_close";
                public const string WikiPagePrefixRequired = "error.assembly_motion.portal.wiki_page_prefix_required";
                public const string WikiAssemblyLabelRequired = "error.assembly_motion.portal.wiki_assembly_label_required";
                public const string OpenSlidesMeetingIdInvalid = "error.assembly_motion.portal.openslides_meeting_id_invalid";
            }

            public static class Type {
                public const string NotFound = "error.assembly_motion.type.not_found";
                public const string CodeInvalid = "error.assembly_motion.type.code_invalid";
                public const string CodeTaken = "error.assembly_motion.type.code_taken";
                public const string NameRequired = "error.assembly_motion.type.name_required";
                public const string MinSubmittersInvalid = "error.assembly_motion.type.min_submitters_invalid";
                public const string CompetingDeadlineInvalid = "error.assembly_motion.type.competing_deadline_invalid";
                public const string HasMotions = "error.assembly_motion.type.has_motions";
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
                public const string IbanInvalid = "error.admin.due_selection.iban_invalid";
                public const string AccountHolderRequired = "error.admin.due_selection.account_holder_required";
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
            public const string MemberImportStarted = "ui.toast.member_import.started";
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
            public const string ChapterPlaceholder = "ui.motion_create.chapter_placeholder";
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
            public const string BoardWorkMailings = "ui.main_nav_bar.board_work.mailings";
            public const string BoardWorkNewsPosts = "ui.main_nav_bar.board_work.news_posts";
            public const string BoardWorkAccreditation = "ui.main_nav_bar.board_work.accreditation";
            public const string BoardWorkTravelExpenseRules = "ui.main_nav_bar.board_work.travel_expense_rules";
            public const string BoardWorkAssemblyMotions = "ui.main_nav_bar.board_work.assembly_motions";
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
            public const string SystemNotificationStats = "ui.main_nav_bar.system.notification_stats";
            public const string SystemPdfTemplates = "ui.main_nav_bar.system.pdf_templates";
            public const string Forms = "ui.main_nav_bar.forms";
            public const string FormsCatalog = "ui.main_nav_bar.forms.catalog";
            public const string FormsMine = "ui.main_nav_bar.forms.mine";
            public const string FormsExpenseReimbursement = "ui.main_nav_bar.forms.expense_reimbursement";
            public const string FormsReceipts = "ui.main_nav_bar.forms.receipts";
            public const string FormsTravelExpense = "ui.main_nav_bar.forms.travel_expense";
            public const string MySessionsTitle = "ui.main_nav_bar.my_sessions_title";
            public const string NotificationsTitle = "ui.main_nav_bar.notifications_title";
            public const string Logout = "ui.main_nav_bar.logout";
            public const string Login = "ui.main_nav_bar.login";
        }

        public static class NewsPosts {
            public const string PageTitle = "ui.news_posts.page_title";
            public const string TabSuggestions = "ui.news_posts.tab_suggestions";
            public const string TabAll = "ui.news_posts.tab_all";
            public const string TabPosted = "ui.news_posts.tab_posted";
            public const string SettingsLink = "ui.news_posts.settings_link";
            public const string TeaserOnly = "ui.news_posts.teaser_only";
            public const string NoCandidates = "ui.news_posts.no_candidates";
            public const string EmptyQueue = "ui.news_posts.empty_queue";
            public const string ProgrammePassage = "ui.news_posts.programme_passage";
            public const string UseCandidate = "ui.news_posts.use_candidate";
            public const string Dismiss = "ui.news_posts.dismiss";
            public const string Regenerate = "ui.news_posts.regenerate";
            public const string Generate = "ui.news_posts.generate";
            public const string Retry = "ui.news_posts.retry";
            public const string Channels = "ui.news_posts.channels";
            public const string ChannelsUnavailable = "ui.news_posts.channels_unavailable";
            public const string PublishNow = "ui.news_posts.publish_now";
            public const string PublishAt = "ui.news_posts.publish_at";
            public const string PublishButton = "ui.news_posts.publish_button";
            public const string PublishConfirm = "ui.news_posts.publish_confirm";
            public const string Published = "ui.news_posts.published";
            public const string Cancel = "ui.news_posts.cancel";
            public const string StatusFilterAll = "ui.news_posts.status_filter_all";
            public const string PostedAt = "ui.news_posts.posted_at";
            public const string PostedBy = "ui.news_posts.posted_by";
            public const string PostFailed = "ui.news_posts.post_failed";
            public const string SettingsTitle = "ui.news_posts.settings_title";
            public const string FeedsTitle = "ui.news_posts.feeds_title";
            public const string FeedName = "ui.news_posts.feed_name";
            public const string FeedUrl = "ui.news_posts.feed_url";
            public const string FeedKind = "ui.news_posts.feed_kind";
            public const string FeedKindRss = "ui.news_posts.feed_kind_rss";
            public const string FeedKindTagesschau = "ui.news_posts.feed_kind_tagesschau";
            public const string FeedEnabled = "ui.news_posts.feed_enabled";
            public const string FeedLastPoll = "ui.news_posts.feed_last_poll";
            public const string FeedAdd = "ui.news_posts.feed_add";
            public const string Save = "ui.news_posts.save";
            public const string Delete = "ui.news_posts.delete";
            public const string SourcesTitle = "ui.news_posts.sources_title";
            public const string SourceTitle = "ui.news_posts.source_title";
            public const string SourceHint = "ui.news_posts.source_hint";
            public const string SourceSections = "ui.news_posts.source_sections";
            public const string SourceLastSync = "ui.news_posts.source_last_sync";
            public const string SourceAdd = "ui.news_posts.source_add";
            public const string SyncNow = "ui.news_posts.sync_now";
            public const string SyncDone = "ui.news_posts.sync_done";
            public const string StatusTitle = "ui.news_posts.status_title";
            public const string StatusEnabled = "ui.news_posts.status_enabled";
            public const string StatusMistral = "ui.news_posts.status_mistral";
            public const string StatusPostiz = "ui.news_posts.status_postiz";
            public const string StatusPostizReachable = "ui.news_posts.status_postiz_reachable";
            public const string StatusLastRun = "ui.news_posts.status_last_run";
            public const string StatusSections = "ui.news_posts.status_sections";
            public const string Yes = "ui.news_posts.yes";
            public const string No = "ui.news_posts.no";
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
            public const string ErrorSsoNoMemberNumberWithSupport = "ui.login.error.sso_no_member_number_with_support";
            public const string ErrorSsoNoMemberNumberNoSupport = "ui.login.error.sso_no_member_number_no_support";
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
            public const string Intro = "ui.home.intro";
            public const string PendingApplicationsTitle = "ui.home.pending_applications_title";
            public const string PendingApplicationsEmpty = "ui.home.pending_applications_empty";
            public const string SubmittedColumn = "ui.home.submitted_column";
            public const string PendingDueSelectionsTitle = "ui.home.pending_due_selections_title";
            public const string PendingDueSelectionsEmpty = "ui.home.pending_due_selections_empty";
            public const string SelectedFeeColumn = "ui.home.selected_fee_column";
            public const string OpenMotionsTitle = "ui.home.open_motions_title";
            public const string OpenMotionsEmpty = "ui.home.open_motions_empty";
            public const string UpcomingEventsTitle = "ui.home.upcoming_events_title";
            public const string UpcomingEventDateOpen = "ui.home.upcoming_event_date_open";
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
            public const string LivesInGermany = "ui.country_selection.lives_in_germany";
            public const string LivesElsewhere = "ui.country_selection.lives_elsewhere";
            public const string GermanyCountryName = "ui.country_selection.germany_country_name";
        }

        public static class MunicipalitySearch {
            public const string EnterManuallyLink = "ui.municipality_search.enter_manually_link";
        }

        public static class AddressDetails {
            public const string CountryLabel = "ui.address_details.country_label";
            public const string PostCodeLabel = "ui.address_details.post_code_label";
            public const string CityLabel = "ui.address_details.city_label";
            public const string FromMunicipalityHint = "ui.address_details.from_municipality_hint";
            public const string StreetLabel = "ui.address_details.street_label";
            public const string HouseNumberLabel = "ui.address_details.house_number_label";
            public const string AssignedChapterTitle = "ui.address_details.assigned_chapter_title";
        }

        public static class DuesTypeSelection {
            public const string Instruction = "ui.dues_type_selection.instruction";
            public const string OnePercentLabel = "ui.dues_type_selection.one_percent_label";
            public const string RecommendedBadge = "ui.dues_type_selection.recommended_badge";
            public const string MonthlyPayLabel = "ui.dues_type_selection.monthly_pay_label";
            public const string UnderageLabel = "ui.dues_type_selection.underage_label";
            public const string UnderageHint = "ui.dues_type_selection.underage_hint";
            public const string ReducedLabel = "ui.dues_type_selection.reduced_label";
        }

        public static class Declarations {
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
            public const string DirectSubmittedNotice = "ui.application_summary.direct_submitted_notice";
            public const string PersonalDataSection = "ui.application_summary.personal_data_section";
            public const string NameLabel = "ui.application_summary.name_label";
            public const string DateOfBirthLabel = "ui.application_summary.date_of_birth_label";
            public const string CitizenshipLabel = "ui.application_summary.citizenship_label";
            public const string EmailLabel = "ui.application_summary.email_label";
            public const string PhoneLabel = "ui.application_summary.phone_label";
            public const string AddressSection = "ui.application_summary.address_section";
            public const string ChapterLabel = "ui.application_summary.chapter_label";
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
            public const string EmailLabel = "ui.due_selector_user_data.email_label";
            public const string MemberNumberLabel = "ui.due_selector_user_data.member_number_label";
            public const string PrefillNotice = "ui.due_selector_user_data.prefill_notice";
        }

        public static class PaymentOptionSelection {
            public const string DirectDepositInstruction = "ui.payment_option_selection.direct_deposit_instruction";
            public const string DirectDepositMandate = "ui.payment_option_selection.direct_deposit_mandate";
            public const string AccountHolderLabel = "ui.payment_option_selection.account_holder_label";
            public const string UseFullNameLink = "ui.payment_option_selection.use_full_name_link";
            public const string IbanLabel = "ui.payment_option_selection.iban_label";
            public const string IbanInvalid = "ui.payment_option_selection.iban_invalid";
            public const string PaymentScheduleHeader = "ui.payment_option_selection.payment_schedule_header";
            public const string ScheduleAnnual = "ui.payment_option_selection.schedule.annual";
            public const string ScheduleQuarterly = "ui.payment_option_selection.schedule.quarterly";
            public const string ScheduleMonthly = "ui.payment_option_selection.schedule.monthly";
        }

        public static class SelectByMonthlyPay {
            public const string IncomeGroupLabel = "ui.select_by_monthly_pay.income_group_label";
            public const string CalculatedDueText = "ui.select_by_monthly_pay.calculated_due_text";
        }

        public static class SelectOnePercentYearlyPay {
            public const string YearlyIncomeLabel = "ui.select_one_percent_yearly_pay.yearly_income_label";
            public const string IncomeTooLowLine1 = "ui.select_one_percent_yearly_pay.income_too_low_line1";
            public const string IncomeTooLowLine2 = "ui.select_one_percent_yearly_pay.income_too_low_line2";
            public const string IncomeTooLowLine3 = "ui.select_one_percent_yearly_pay.income_too_low_line3";
            public const string CalculatedYearlyDueLabel = "ui.select_one_percent_yearly_pay.calculated_yearly_due_label";
        }

        public static class SelectReduced {
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
            public const string RecipientPreviewAria = "ui.event_checklist_editor.recipient_preview_aria";
            public const string RecipientPreviewHeader = "ui.event_checklist_editor.recipient_preview_header";
            public const string RecipientPreviewEmpty = "ui.event_checklist_editor.recipient_preview_empty";
            public const string RecipientPreviewFailed = "ui.event_checklist_editor.recipient_preview_failed";
            public const string SendDisabledNoContent = "ui.event_checklist_editor.send_disabled_no_content";
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
            public const string FloatingFilterLabel = "ui.member_list.floating_filter_label";
            public const string FloatingBadge = "ui.member_list.floating_badge";
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
            public const string FloatingBadge = "ui.member_detail.floating_badge";
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
            public const string SkippedColumn = "ui.member_import_history.skipped_column";
            public const string NewFloatingColumn = "ui.member_import_history.new_floating_column";
            public const string UpdatedFloatingColumn = "ui.member_import_history.updated_floating_column";
            public const string PromotedColumn = "ui.member_import_history.promoted_column";
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
            public const string SkippedLabel = "ui.import_status.skipped_label";
            public const string NewFloatingLabel = "ui.import_status.new_floating_label";
            public const string UpdatedFloatingLabel = "ui.import_status.updated_floating_label";
            public const string PromotedLabel = "ui.import_status.promoted_label";
            public const string ErrorsLabel = "ui.import_status.errors_label";
            public const string HistorySummary = "ui.import_status.history_summary";
            public const string NoImportsYet = "ui.import_status.no_imports_yet";
            public const string AdminDivImportSection = "ui.import_status.admin_div_import_section";
            public const string AddedLabel = "ui.import_status.added_label";
            public const string RemovedLabel = "ui.import_status.removed_label";
            public const string RemappedLabel = "ui.import_status.remapped_label";
            public const string OrphanedLabel = "ui.import_status.orphaned_label";
        }

        public static class Mailings {
            public const string PageTitle = "ui.mailings.page_title";
            public const string NewMailButton = "ui.mailings.new_mail_button";
            public const string IncludeArchivedToggle = "ui.mailings.include_archived_toggle";
            public const string TitleColumn = "ui.mailings.title_column";
            public const string ChapterColumn = "ui.mailings.chapter_column";
            public const string TargetColumn = "ui.mailings.target_column";
            public const string StatusColumn = "ui.mailings.status_column";
            public const string CreatedColumn = "ui.mailings.created_column";
            public const string SentColumn = "ui.mailings.sent_column";
            public const string RecipientsColumn = "ui.mailings.recipients_column";
            public const string StatusDraft = "ui.mailings.status_draft";
            public const string StatusSent = "ui.mailings.status_sent";
            public const string StatusArchived = "ui.mailings.status_archived";
            public const string TargetTypeChapter = "ui.mailings.target_type_chapter";
            public const string TargetTypeAdminDivision = "ui.mailings.target_type_admin_division";
            public const string NoMailings = "ui.mailings.no_mailings";
            public const string NewMailDialogTitle = "ui.mailings.new_mail_dialog_title";
            public const string NewMailChapterLabel = "ui.mailings.new_mail_chapter_label";
            public const string NewMailTitleLabel = "ui.mailings.new_mail_title_label";
            public const string CreateButton = "ui.mailings.create_button";
            public const string EditTitleLabel = "ui.mailings.edit_title_label";
            public const string TargetSection = "ui.mailings.target_section";
            public const string TargetTypeLabel = "ui.mailings.target_type_label";
            public const string TargetIdLabel = "ui.mailings.target_id_label";
            public const string IncludeDescendantsLabel = "ui.mailings.include_descendants_label";
            public const string SubjectLabel = "ui.mailings.subject_label";
            public const string ModeLabel = "ui.mailings.mode_label";
            public const string ModeTemplate = "ui.mailings.mode_template";
            public const string ModeManual = "ui.mailings.mode_manual";
            public const string TemplateLabel = "ui.mailings.template_label";
            public const string ContentLabel = "ui.mailings.content_label";
            public const string SaveButton = "ui.mailings.save_button";
            public const string RecipientPreviewButton = "ui.mailings.recipient_preview_button";
            public const string SendButton = "ui.mailings.send_button";
            public const string DeleteButton = "ui.mailings.delete_button";
            public const string BackToList = "ui.mailings.back_to_list";
            public const string SentNoticePrefix = "ui.mailings.sent_notice_prefix";
            public const string ArchivedNoticePrefix = "ui.mailings.archived_notice_prefix";
            public const string SendConfirmTitle = "ui.mailings.send_confirm_title";
            public const string SendConfirmBody = "ui.mailings.send_confirm_body";
            public const string DeleteConfirmDraft = "ui.mailings.delete_confirm_draft";
            public const string DeleteConfirmSent = "ui.mailings.delete_confirm_sent";
            public const string RecipientCountHeader = "ui.mailings.recipient_count_header";
            public const string RecipientsEmpty = "ui.mailings.recipients_empty";
            public const string SendDisabledMissingTemplate = "ui.mailings.send_disabled_missing_template";
            public const string SendDisabledMissingManualContent = "ui.mailings.send_disabled_missing_manual_content";
        }

        public static class PdfTemplates {
            public const string PageTitle = "ui.pdf_templates.page_title";
            public const string PageIntro = "ui.pdf_templates.page_intro";
            public const string FormColumn = "ui.pdf_templates.form_column";
            public const string UpdatedColumn = "ui.pdf_templates.updated_column";
            public const string EditAction = "ui.pdf_templates.edit_action";
            public const string NeverEdited = "ui.pdf_templates.never_edited";
            public const string EditorTitle = "ui.pdf_templates.editor_title";
            public const string EditorIntro = "ui.pdf_templates.editor_intro";
            public const string SaveButton = "ui.pdf_templates.save_button";
            public const string ResetButton = "ui.pdf_templates.reset_button";
            public const string ResetConfirm = "ui.pdf_templates.reset_confirm";
            public const string RefreshPreviewButton = "ui.pdf_templates.refresh_preview_button";
            public const string AutoPreviewLabel = "ui.pdf_templates.auto_preview_label";
            public const string PreviewHeading = "ui.pdf_templates.preview_heading";
            public const string PreviewTab = "ui.pdf_templates.preview_tab";
            public const string FieldsTab = "ui.pdf_templates.fields_tab";
            public const string PreviewError = "ui.pdf_templates.preview_error";
        }

        public static class NotificationStats {
            public const string PageTitle = "ui.notification_stats.page_title";
            public const string FilterFrom = "ui.notification_stats.filter_from";
            public const string FilterTo = "ui.notification_stats.filter_to";
            public const string FilterChannel = "ui.notification_stats.filter_channel";
            public const string FilterTrigger = "ui.notification_stats.filter_trigger";
            public const string FilterTemplate = "ui.notification_stats.filter_template";
            public const string FilterSourceEntityType = "ui.notification_stats.filter_source_entity_type";
            public const string FilterGroupBy = "ui.notification_stats.filter_group_by";
            public const string ApplyButton = "ui.notification_stats.apply_button";
            public const string ResetButton = "ui.notification_stats.reset_button";
            public const string SummarySent = "ui.notification_stats.summary_sent";
            public const string SummaryFailed = "ui.notification_stats.summary_failed";
            public const string SummaryMdnRequested = "ui.notification_stats.summary_mdn_requested";
            public const string SummaryMdnReceived = "ui.notification_stats.summary_mdn_received";
            public const string SummaryBounced = "ui.notification_stats.summary_bounced";
            public const string SummaryReadRate = "ui.notification_stats.summary_read_rate";
            public const string SummaryAverageTimeToRead = "ui.notification_stats.summary_average_time_to_read";
            public const string GroupByNone = "ui.notification_stats.group_by_none";
            public const string GroupByTrigger = "ui.notification_stats.group_by_trigger";
            public const string GroupByTemplate = "ui.notification_stats.group_by_template";
            public const string GroupBySourceEntityType = "ui.notification_stats.group_by_source_entity_type";
            public const string GroupByChannel = "ui.notification_stats.group_by_channel";
            public const string BreakdownTableHeaderKey = "ui.notification_stats.breakdown_table_header_key";
            public const string BreakdownTableHeaderSent = "ui.notification_stats.breakdown_table_header_sent";
            public const string BreakdownTableHeaderMdnRequested = "ui.notification_stats.breakdown_table_header_mdn_requested";
            public const string BreakdownTableHeaderMdnReceived = "ui.notification_stats.breakdown_table_header_mdn_received";
            public const string BreakdownTableHeaderBounced = "ui.notification_stats.breakdown_table_header_bounced";
            public const string BreakdownTableHeaderReadRate = "ui.notification_stats.breakdown_table_header_read_rate";
            public const string NoData = "ui.notification_stats.no_data";
            public const string EmptyKey = "ui.notification_stats.empty_key";
            public const string FilterAnyChannel = "ui.notification_stats.filter_any_channel";
            public const string FilterAnyTrigger = "ui.notification_stats.filter_any_trigger";
            public const string FilterAnyTemplate = "ui.notification_stats.filter_any_template";
            public const string FilterAnySourceEntity = "ui.notification_stats.filter_any_source_entity";
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
            public const string KeycloakDepartmentLabel = "ui.chapter_detail.keycloak_department_label";
            public const string KeycloakDepartmentHint = "ui.chapter_detail.keycloak_department_hint";
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
            public const string ArchivedBadge = "ui.chapter_detail.archived_badge";
            public const string ArchiveButton = "ui.chapter_detail.archive_button";
            public const string UnarchiveButton = "ui.chapter_detail.unarchive_button";
            public const string ArchiveDialogTitle = "ui.chapter_detail.archive_dialog_title";
            public const string ArchiveDialogMessage = "ui.chapter_detail.archive_dialog_message";
            public const string UnarchiveDialogTitle = "ui.chapter_detail.unarchive_dialog_title";
            public const string UnarchiveDialogMessage = "ui.chapter_detail.unarchive_dialog_message";
            public const string ArchiveDialogConfirm = "ui.chapter_detail.archive_dialog_confirm";
            public const string UnarchiveDialogConfirm = "ui.chapter_detail.unarchive_dialog_confirm";
            public const string OfficerActionColumn = "ui.chapter_detail.officer_action_column";
            public const string RemoveOfficerAriaLabel = "ui.chapter_detail.remove_officer_aria_label";
            public const string RemoveOfficerDialogTitle = "ui.chapter_detail.remove_officer_dialog_title";
            public const string RemoveOfficerDialogMessage = "ui.chapter_detail.remove_officer_dialog_message";
            public const string RemoveOfficerDialogConfirm = "ui.chapter_detail.remove_officer_dialog_confirm";
        }

        public static class ChapterCreate {
            public const string PageTitle = "ui.chapter_create.page_title";
            public const string ShortCodeLabel = "ui.chapter_create.short_code_label";
            public const string ExternalCodeLabel = "ui.chapter_create.external_code_label";
            public const string KeycloakDepartmentLabel = "ui.chapter_create.keycloak_department_label";
            public const string KeycloakDepartmentHint = "ui.chapter_create.keycloak_department_hint";
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
            public const string AddFromKeycloak = "ui.user_list.add_from_keycloak";
        }

        public static class UserKeycloakAdd {
            public const string PageTitle = "ui.user_keycloak_add.page_title";
            public const string Back = "ui.user_keycloak_add.back";
            public const string Intro = "ui.user_keycloak_add.intro";
            public const string NotConfigured = "ui.user_keycloak_add.not_configured";
            public const string NoAccount = "ui.user_keycloak_add.no_account";
            public const string Username = "ui.user_keycloak_add.username";
            public const string MemberNumber = "ui.user_keycloak_add.member_number";
            public const string Chapter = "ui.user_keycloak_add.chapter";
            public const string UnmappedDepartment = "ui.user_keycloak_add.unmapped_department";
            public const string OpenUser = "ui.user_keycloak_add.open_user";
            public const string Add = "ui.user_keycloak_add.add";
            public const string Added = "ui.user_keycloak_add.added";
            public const string MoreResults = "ui.user_keycloak_add.more_results";
        }

        public static class KeycloakAccountSearch {
            public const string MemberNumber = "ui.keycloak_account_search.member_number";
            public const string Query = "ui.keycloak_account_search.query";
            public const string QueryPlaceholder = "ui.keycloak_account_search.query_placeholder";
            public const string Search = "ui.keycloak_account_search.search";
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
            public const string ScopeOverrideBadge = "ui.user_detail.scope_override_badge";
            public const string ScopeOverrideTooltip = "ui.user_detail.scope_override_tooltip";
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

        public static class ImapSetup {
            public const string PageTitle = "ui.imap_setup.page_title";
            public const string ServerSettingsTitle = "ui.imap_setup.server_settings_title";
            public const string MdnSectionTitle = "ui.imap_setup.mdn_section_title";
            public const string CopyFromSmtpButton = "ui.imap_setup.copy_from_smtp_button";
            public const string ReceiptFallbackHint = "ui.imap_setup.receipt_fallback_hint";
        }

        public static class AssemblyLog {
            public const string Title = "ui.assembly_log.title";
            public const string Intro = "ui.assembly_log.intro";
            public const string NoSessions = "ui.assembly_log.no_sessions";
            public const string Running = "ui.assembly_log.running";
            public const string Deleted = "ui.assembly_log.deleted";
            public const string Entries = "ui.assembly_log.entries";
            public const string Csv = "ui.assembly_log.csv";
            public const string Markdown = "ui.assembly_log.markdown";
            public const string Delete = "ui.assembly_log.delete";
            public const string DeleteConfirm = "ui.assembly_log.delete_confirm";
            public const string ColumnTime = "ui.assembly_log.column.time";
            public const string ColumnKind = "ui.assembly_log.column.kind";
            public const string ColumnItem = "ui.assembly_log.column.item";
            public const string ColumnName = "ui.assembly_log.column.name";
            public const string ColumnRole = "ui.assembly_log.column.role";
            public const string ColumnStart = "ui.assembly_log.column.start";
            public const string ColumnEnd = "ui.assembly_log.column.end";
            public const string ColumnDuration = "ui.assembly_log.column.duration";
            public const string ColumnAllotted = "ui.assembly_log.column.allotted";
            public const string ColumnPoll = "ui.assembly_log.column.poll";
            public const string ColumnDetail = "ui.assembly_log.column.detail";
            public const string KindSessionStarted = "ui.assembly_log.kind.session_started";
            public const string KindSessionEnded = "ui.assembly_log.kind.session_ended";
            public const string KindSpeech = "ui.assembly_log.kind.speech";
            public const string KindChairAction = "ui.assembly_log.kind.chair_action";
            public const string KindPhaseChange = "ui.assembly_log.kind.phase_change";
            public const string KindPoll = "ui.assembly_log.kind.poll";
            public const string KindItemChange = "ui.assembly_log.kind.item_change";
            public const string MarkdownTitle = "ui.assembly_log.markdown_title";
            public const string MarkdownSession = "ui.assembly_log.markdown_session";
            public const string NoItem = "ui.assembly_log.no_item";
            public const string SpeechLine = "ui.assembly_log.speech_line";
            public const string SpeechLineUntimed = "ui.assembly_log.speech_line_untimed";

            public static class Error {
                public const string SessionRunning = "ui.assembly_log.error.session_running";
            }
        }

        public static class AssemblyPollResult {
            public const string Accepted = "ui.assembly_poll_result.accepted";
            public const string Rejected = "ui.assembly_poll_result.rejected";
            public const string TwoThirdsReached = "ui.assembly_poll_result.two_thirds_reached";
            public const string TwoThirdsMissed = "ui.assembly_poll_result.two_thirds_missed";
            public const string ThresholdReached = "ui.assembly_poll_result.threshold_reached";
            public const string ThresholdMissed = "ui.assembly_poll_result.threshold_missed";
            public const string Numbers = "ui.assembly_poll_result.numbers";
            public const string AcceptanceWinner = "ui.assembly_poll_result.acceptance_winner";
            public const string RunoffNeeded = "ui.assembly_poll_result.runoff_needed";
        }

        public static class AssemblyOverlay {
            public const string PollRunning = "ui.assembly_overlay.poll_running";
            public const string PollResult = "ui.assembly_overlay.poll_result";
            public const string Votes = "ui.assembly_overlay.votes";
            public const string ElectionRunning = "ui.assembly_overlay.election_running";
            public const string NextPro = "ui.assembly_overlay.next_pro";
            public const string NextContra = "ui.assembly_overlay.next_contra";
        }

        public static class AssemblyConsole {
            public const string TemplatesTitle = "ui.assembly_console.templates_title";
            public const string TemplateText = "ui.assembly_console.template_text";
            public const string TemplateContinueDebate = "ui.assembly_console.template.continue_debate";
            public const string TemplateGoMotion = "ui.assembly_console.template.go_motion";
            public const string TemplateOpinion = "ui.assembly_console.template.opinion";
            public const string TemplateSupport = "ui.assembly_console.template.support";
            public const string TemplateControversy = "ui.assembly_console.template.controversy";
            public const string TemplateCompetition = "ui.assembly_console.template.competition";
            public const string TemplateRunoff = "ui.assembly_console.template.runoff";
            public const string TemplateFinalVote = "ui.assembly_console.template.final_vote";
            public const string TemplateBlock = "ui.assembly_console.template.block";
            public const string TemplateQuestionRound = "ui.assembly_console.template.question_round";
            public const string SupportThreshold = "ui.assembly_console.support_threshold";
            public const string CreateProceduralTopics = "ui.assembly_console.create_procedural_topics";
            public const string ResetPoll = "ui.assembly_console.reset_poll";
            public const string DeletePoll = "ui.assembly_console.delete_poll";
            public const string ResetConfirm = "ui.assembly_console.reset_confirm";
            public const string DeleteConfirm = "ui.assembly_console.delete_confirm";
            public const string ProjectPolls = "ui.assembly_console.project_polls";
            public const string CandidatesTitle = "ui.assembly_console.candidates_title";
            public const string ApplyCandidates = "ui.assembly_console.apply_candidates";
            public const string CandidatePresent = "ui.assembly_console.candidate_present";
            public const string CandidateSupport = "ui.assembly_console.candidate_support";
            public const string FlowTitle = "ui.assembly_console.flow_title";
            public const string RoundInfo = "ui.assembly_console.round_info";
            public const string SideCounter = "ui.assembly_console.side_counter";
            public const string RoundComplete = "ui.assembly_console.round_complete";
            public const string ContinueDebate = "ui.assembly_console.continue_debate";
            public const string ToClosingWord = "ui.assembly_console.to_closing_word";
            public const string GoTitle = "ui.assembly_console.go_title";
            public const string GoAccept = "ui.assembly_console.go_accept";
            public const string GoObjectionButton = "ui.assembly_console.go_objection";
            public const string GoObjectionHint = "ui.assembly_console.go_objection_hint";
            public const string GoEnd = "ui.assembly_console.go_end";
            public const string RepeatPresentation = "ui.assembly_console.repeat_presentation";
            public const string ElectionOverlay = "ui.assembly_console.election_overlay";
            public const string QuestionersInfo = "ui.assembly_console.questioners";
            public const string PollsTitle = "ui.assembly_console.polls_title";
            public const string OpenSlidesDisabled = "ui.assembly_console.openslides_disabled";
            public const string OpenSlidesConnected = "ui.assembly_console.openslides_connected";
            public const string OpenSlidesConnecting = "ui.assembly_console.openslides_connecting";
            public const string OpenSlidesDisconnected = "ui.assembly_console.openslides_disconnected";
            public const string PollDuration = "ui.assembly_console.poll_duration";
            public const string AutoLabel = "ui.assembly_console.auto_label";
            public const string AutoStart = "ui.assembly_console.auto_start";
            public const string AutoStop = "ui.assembly_console.auto_stop";
            public const string AutoPublish = "ui.assembly_console.auto_publish";
            public const string NoPolls = "ui.assembly_console.no_polls";
            public const string PollNotStarted = "ui.assembly_console.poll_not_started";
            public const string VotesSoFar = "ui.assembly_console.votes_so_far";
            public const string ExtendPoll = "ui.assembly_console.extend_poll";
            public const string StopPoll = "ui.assembly_console.stop_poll";
            public const string PublishPoll = "ui.assembly_console.publish_poll";
            public const string PollStateFinished = "ui.assembly_console.poll_state_finished";
            public const string PollStatePublished = "ui.assembly_console.poll_state_published";
            public const string AgendaPick = "ui.assembly_console.agenda_pick";
            public const string NotSignedIn = "ui.assembly_console.not_signed_in";
            public const string SignIn = "ui.assembly_console.sign_in";
            public const string SecondsShort = "ui.assembly_console.seconds_short";
            public const string MotionMinutesShort = "ui.assembly_console.motion_minutes_short";
            public const string MotionMinutesDefault = "ui.assembly_console.motion_minutes_default";
            public const string MinutesValue = "ui.assembly_console.minutes_value";
            public const string PickerHint = "ui.assembly_console.picker_hint";
            public const string PickerNone = "ui.assembly_console.picker_none";
            public const string NoNotices = "ui.assembly_console.no_notices";
            public const string PageTitle = "ui.assembly_console.page_title";
            public const string OpenConsole = "ui.assembly_console.open_console";
            public const string NoAccess = "ui.assembly_console.no_access";
            public const string NoSession = "ui.assembly_console.no_session";
            public const string OtherSession = "ui.assembly_console.other_session";
            public const string StartSession = "ui.assembly_console.start_session";
            public const string EndSession = "ui.assembly_console.end_session";
            public const string EndConfirm = "ui.assembly_console.end_confirm";
            public const string ReadOnly = "ui.assembly_console.read_only";
            public const string MumbleState = "ui.assembly_console.mumble_state";
            public const string StateConnecting = "ui.assembly_console.state_connecting";
            public const string StateSynced = "ui.assembly_console.state_synced";
            public const string StateReconnecting = "ui.assembly_console.state_reconnecting";
            public const string StateDisconnected = "ui.assembly_console.state_disconnected";
            public const string Item = "ui.assembly_console.item";
            public const string ItemPlaceholder = "ui.assembly_console.item_placeholder";
            public const string SetItem = "ui.assembly_console.set_item";
            public const string MotionMinutes = "ui.assembly_console.motion_minutes";
            public const string Apply = "ui.assembly_console.apply";
            public const string Clear = "ui.assembly_console.clear";
            public const string ListStatus = "ui.assembly_console.list_status";
            public const string ListOpen = "ui.assembly_console.list_open";
            public const string ListClosed = "ui.assembly_console.list_closed";
            public const string ListClosesAt = "ui.assembly_console.list_closes_at";
            public const string CloseNow = "ui.assembly_console.close_now";
            public const string CloseAt = "ui.assembly_console.close_at";
            public const string Reopen = "ui.assembly_console.reopen";
            public const string FloorTitle = "ui.assembly_console.floor_title";
            public const string NobodyOnFloor = "ui.assembly_console.nobody_on_floor";
            public const string Done = "ui.assembly_console.done";
            public const string AddMinute = "ui.assembly_console.add_minute";
            public const string Pause = "ui.assembly_console.pause";
            public const string Resume = "ui.assembly_console.resume";
            public const string StartNow = "ui.assembly_console.start_now";
            public const string SetTime = "ui.assembly_console.set_time";
            public const string Untimed = "ui.assembly_console.untimed";
            public const string WaitingForVoice = "ui.assembly_console.waiting_for_voice";
            public const string Paused = "ui.assembly_console.paused";
            public const string DonePending = "ui.assembly_console.done_pending";
            public const string Speaking = "ui.assembly_console.speaking";
            public const string QueuePoint = "ui.assembly_console.queue_point";
            public const string QueueQuestion = "ui.assembly_console.queue_question";
            public const string QueuePro = "ui.assembly_console.queue_pro";
            public const string QueueContra = "ui.assembly_console.queue_contra";
            public const string QueueEmpty = "ui.assembly_console.queue_empty";
            public const string NotMapped = "ui.assembly_console.not_mapped";
            public const string GiveFloor = "ui.assembly_console.give_floor";
            public const string Skip = "ui.assembly_console.skip";
            public const string Unskip = "ui.assembly_console.unskip";
            public const string Dismiss = "ui.assembly_console.dismiss";
            public const string UndoDismiss = "ui.assembly_console.undo_dismiss";
            public const string Late = "ui.assembly_console.late";
            public const string Uncertain = "ui.assembly_console.uncertain";
            public const string Skipped = "ui.assembly_console.skipped";
            public const string Dismissed = "ui.assembly_console.dismissed";
            public const string Pending = "ui.assembly_console.pending";
            public const string Suggested = "ui.assembly_console.suggested";
            public const string PickerTitle = "ui.assembly_console.picker_title";
            public const string PickerSearch = "ui.assembly_console.picker_search";
            public const string ToSpeaker = "ui.assembly_console.to_speaker";
            public const string ToMotionAuthor = "ui.assembly_console.to_motion_author";
            public const string NoticesTitle = "ui.assembly_console.notices_title";
            public const string Shortcuts = "ui.assembly_console.shortcuts";
            public const string RoleManual = "ui.assembly_console.role_manual";
            public const string RolePro = "ui.assembly_console.role_pro";
            public const string RoleContra = "ui.assembly_console.role_contra";
            public const string RoleQuestion = "ui.assembly_console.role_question";
            public const string RolePoint = "ui.assembly_console.role_point";
            public const string RoleMotionAuthor = "ui.assembly_console.role_motion_author";
            public const string RoleCandidate = "ui.assembly_console.role_candidate";

            public static class Reject {
                public const string NoAgendaItem = "ui.assembly_console.reject.no_agenda_item";
                public const string DelegatesGroupMissing = "ui.assembly_console.reject.delegates_group_missing";
                public const string TextRequired = "ui.assembly_console.reject.text_required";
                public const string NotAMotion = "ui.assembly_console.reject.not_a_motion";
                public const string NoCompetitors = "ui.assembly_console.reject.no_competitors";
                public const string NotOwnPoll = "ui.assembly_console.reject.not_own_poll";
                public const string OpenSlidesFailed = "ui.assembly_console.reject.openslides_failed";
                public const string AgendaItemNotFound = "ui.assembly_console.reject.agenda_item_not_found";
                public const string PollNotFound = "ui.assembly_console.reject.poll_not_found";
                public const string NoRunningPoll = "ui.assembly_console.reject.no_running_poll";
                public const string Stale = "ui.assembly_console.reject.stale";
                public const string NoSession = "ui.assembly_console.reject.no_session";
                public const string EventNotActive = "ui.assembly_console.reject.event_not_active";
                public const string EntryNotFound = "ui.assembly_console.reject.entry_not_found";
                public const string UserNotFound = "ui.assembly_console.reject.user_not_found";
                public const string MovePending = "ui.assembly_console.reject.move_pending";
                public const string NotOnFloor = "ui.assembly_console.reject.not_on_floor";
                public const string Untimed = "ui.assembly_console.reject.untimed";
                public const string InvalidTime = "ui.assembly_console.reject.invalid_time";
                public const string InvalidThreshold = "ui.assembly_console.reject.invalid_threshold";
                public const string InvalidTarget = "ui.assembly_console.reject.invalid_target";
                public const string ChannelNotMapped = "ui.assembly_console.reject.channel_not_mapped";
                public const string NotConnected = "ui.assembly_console.reject.not_connected";
                public const string SessionRunning = "ui.assembly_console.reject.session_running";
                public const string NotConfigured = "ui.assembly_console.reject.not_configured";
            }

            public static class Phase {
                public const string None = "ui.assembly_console.phase.none";
                public const string Presentation = "ui.assembly_console.phase.presentation";
                public const string Questions = "ui.assembly_console.phase.questions";
                public const string Debate = "ui.assembly_console.phase.debate";
                public const string RoundEnd = "ui.assembly_console.phase.round_end";
                public const string Statement = "ui.assembly_console.phase.statement";
                public const string ClosingWord = "ui.assembly_console.phase.closing_word";
                public const string Vote = "ui.assembly_console.phase.vote";
                public const string Candidacies = "ui.assembly_console.phase.candidacies";
                public const string CandidatePresentation = "ui.assembly_console.phase.candidate_presentation";
                public const string CandidateQuestions = "ui.assembly_console.phase.candidate_questions";
                public const string ElectionRunning = "ui.assembly_console.phase.election_running";
                public const string GeneralDebate = "ui.assembly_console.phase.general_debate";
            }

            public static class Notice {
                public const string PollCreated = "ui.assembly_console.notice.poll_created";
                public const string ProceduralTopicsCreated = "ui.assembly_console.notice.procedural_topics_created";
                public const string PollReset = "ui.assembly_console.notice.poll_reset";
                public const string PollDeleted = "ui.assembly_console.notice.poll_deleted";
                public const string CandidatesSet = "ui.assembly_console.notice.candidates_set";
                public const string CurrentCandidate = "ui.assembly_console.notice.current_candidate";
                public const string ElectionStarted = "ui.assembly_console.notice.election_started";
                public const string ElectionStopped = "ui.assembly_console.notice.election_stopped";
                public const string RepeatPresentationOn = "ui.assembly_console.notice.repeat_on";
                public const string RepeatPresentationOff = "ui.assembly_console.notice.repeat_off";
                public const string NextRound = "ui.assembly_console.notice.next_round";
                public const string GoAccepted = "ui.assembly_console.notice.go_accepted";
                public const string GoObjection = "ui.assembly_console.notice.go_objection";
                public const string GoEnded = "ui.assembly_console.notice.go_ended";
                public const string OpenSlidesActionFailed = "ui.assembly_console.notice.openslides_action_failed";
                public const string OpenSlidesLost = "ui.assembly_console.notice.openslides_lost";
                public const string OpenSlidesConnected = "ui.assembly_console.notice.openslides_connected";
                public const string PollPublished = "ui.assembly_console.notice.poll_published";
                public const string PollPublishRequested = "ui.assembly_console.notice.poll_publish_requested";
                public const string PollStopRequested = "ui.assembly_console.notice.poll_stop_requested";
                public const string PollExtended = "ui.assembly_console.notice.poll_extended";
                public const string PollSettingsChanged = "ui.assembly_console.notice.poll_settings_changed";
                public const string GaveFloor = "ui.assembly_console.notice.gave_floor";
                public const string ManualFloor = "ui.assembly_console.notice.manual_floor";
                public const string Done = "ui.assembly_console.notice.done";
                public const string MoveFailed = "ui.assembly_console.notice.move_failed";
                public const string Skipped = "ui.assembly_console.notice.skipped";
                public const string Unskipped = "ui.assembly_console.notice.unskipped";
                public const string Dismissed = "ui.assembly_console.notice.dismissed";
                public const string DismissUndone = "ui.assembly_console.notice.dismiss_undone";
                public const string TimeAdded = "ui.assembly_console.notice.time_added";
                public const string TimeSet = "ui.assembly_console.notice.time_set";
                public const string MotionTimeSet = "ui.assembly_console.notice.motion_time_set";
                public const string MotionTimeCleared = "ui.assembly_console.notice.motion_time_cleared";
                public const string TimerPaused = "ui.assembly_console.notice.timer_paused";
                public const string TimerResumed = "ui.assembly_console.notice.timer_resumed";
                public const string TimerStarted = "ui.assembly_console.notice.timer_started";
                public const string ListClosed = "ui.assembly_console.notice.list_closed";
                public const string ListClosesAt = "ui.assembly_console.notice.list_closes_at";
                public const string ListReopened = "ui.assembly_console.notice.list_reopened";
                public const string ItemChanged = "ui.assembly_console.notice.item_changed";
                public const string BotMovedBack = "ui.assembly_console.notice.bot_moved_back";
                public const string ConnectionLost = "ui.assembly_console.notice.connection_lost";
                public const string ConnectionRestored = "ui.assembly_console.notice.connection_restored";
                public const string PermissionDenied = "ui.assembly_console.notice.permission_denied";
                public const string SessionStarted = "ui.assembly_console.notice.session_started";
            }
        }

        public static class AssemblySetup {
            public const string PageTitle = "ui.assembly_setup.page_title";
            public const string Intro = "ui.assembly_setup.intro";
            public const string ConnectionTitle = "ui.assembly_setup.connection_title";
            public const string CertificateTitle = "ui.assembly_setup.certificate_title";
            public const string CertificateMissing = "ui.assembly_setup.certificate_missing";
            public const string CertificateUnreadable = "ui.assembly_setup.certificate_unreadable";
            public const string CertificateHash = "ui.assembly_setup.certificate_hash";
            public const string CertificateValidUntil = "ui.assembly_setup.certificate_valid_until";
            public const string GenerateButton = "ui.assembly_setup.generate_button";
            public const string ReplaceButton = "ui.assembly_setup.replace_button";
            public const string ReplaceConfirm = "ui.assembly_setup.replace_confirm";
            public const string CertificateGenerated = "ui.assembly_setup.certificate_generated";
            public const string CheckButton = "ui.assembly_setup.check_button";
            public const string RegisterButton = "ui.assembly_setup.register_button";
            public const string Registered = "ui.assembly_setup.registered";
            public const string NotRegistered = "ui.assembly_setup.not_registered";
            public const string RegisterDone = "ui.assembly_setup.register_done";
            public const string ServerVersion = "ui.assembly_setup.server_version";
            public const string ServerFingerprint = "ui.assembly_setup.server_fingerprint";
            public const string ChannelsTitle = "ui.assembly_setup.channels_title";
            public const string ChannelsHint = "ui.assembly_setup.channels_hint";
            public const string ChannelNone = "ui.assembly_setup.channel_none";
            public const string SaveChannelsButton = "ui.assembly_setup.save_channels_button";
            public const string CheckFirst = "ui.assembly_setup.check_first";
            public const string AclTitle = "ui.assembly_setup.acl_title";
            public const string AclHint = "ui.assembly_setup.acl_hint";
            public const string NoProblems = "ui.assembly_setup.no_problems";

            public static class Role {
                public const string Chair = "ui.assembly_setup.role.chair";
                public const string Speaker = "ui.assembly_setup.role.speaker";
                public const string MotionAuthor = "ui.assembly_setup.role.motion_author";
                public const string Pro = "ui.assembly_setup.role.pro";
                public const string Contra = "ui.assembly_setup.role.contra";
                public const string Question = "ui.assembly_setup.role.question";
                public const string Point = "ui.assembly_setup.role.point";
                public const string Listener = "ui.assembly_setup.role.listener";
            }

            public static class Problem {
                public const string Unmapped = "ui.assembly_setup.problem.unmapped";
                public const string ChannelMissing = "ui.assembly_setup.problem.channel_missing";
                public const string NotLinkedToChair = "ui.assembly_setup.problem.not_linked_to_chair";
                public const string DuplicateChannel = "ui.assembly_setup.problem.duplicate_channel";
            }

            public static class Error {
                public const string NotConfigured = "ui.assembly_setup.error.not_configured";
                public const string ConnectionFailed = "ui.assembly_setup.error.connection_failed";
                public const string Timeout = "ui.assembly_setup.error.timeout";
                public const string RegisterFailed = "ui.assembly_setup.error.register_failed";
                public const string CertificateExists = "ui.assembly_setup.error.certificate_exists";
                public const string AllRolesRequired = "ui.assembly_setup.error.all_roles_required";
                public const string SessionRunning = "ui.assembly_setup.error.session_running";
            }
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
            public const string ModelExpenseReimbursement = "ui.template_field_palette.model_expense_reimbursement";
            public const string ModelTravelExpense = "ui.template_field_palette.model_travel_expense";
            public const string ModelAssemblyMotionNotification = "ui.template_field_palette.model_assembly_motion_notification";
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
            public const string PdfPreviewUnsavedTooltip = "ui.template_detail.pdf_preview_unsaved_tooltip";
            public const string RenderModeLabel = "ui.template_detail.render_mode_label";
            public const string RenderModeSimple = "ui.template_detail.render_mode_simple";
            public const string RenderModeEnvelope = "ui.template_detail.render_mode_envelope";
            public const string InsertEnvelopeTagsButton = "ui.template_detail.insert_envelope_tags_button";
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
            public const string IdentifierColumn = "ui.option_list.identifier_column";
            public const string TypeColumn = "ui.option_list.type_column";
            public const string OverridableColumn = "ui.option_list.overridable_column";
            public const string OverridesColumn = "ui.option_list.overrides_column";
            public const string DataTypeString = "ui.option_list.data_type.string";
            public const string DataTypeNumber = "ui.option_list.data_type.number";
            public const string DataTypeUnknown = "ui.option_list.data_type.unknown";
            public const string Intro = "ui.option_list.intro";
            public const string SearchPlaceholder = "ui.option_list.search_placeholder";
            public const string AssistantColumn = "ui.option_list.assistant_column";
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
            public const string DataSource = "ui.municipality_picker.data_source";
        }

        public static class Footer {
            public const string OpenSource = "ui.footer.open_source";
            public const string Attributions = "ui.footer.attributions";
        }

        public static class Attributions {
            public const string PageTitle = "ui.attributions.page_title";
            public const string OwnLicense = "ui.attributions.own_license";
            public const string GeoNamesText = "ui.attributions.geonames_text";
            public const string CcByLicenseLink = "ui.attributions.cc_by_license_link";
            public const string FontsHeading = "ui.attributions.fonts_heading";
            public const string FontsText = "ui.attributions.fonts_text";
            public const string FontsLink = "ui.attributions.fonts_link";
            public const string LibrariesHeading = "ui.attributions.libraries_heading";
            public const string LibrariesText = "ui.attributions.libraries_text";
            public const string ScopeServer = "ui.attributions.scope_server";
            public const string ScopeBrowser = "ui.attributions.scope_browser";
            public const string ColumnProject = "ui.attributions.column_project";
            public const string ColumnLicense = "ui.attributions.column_license";
        }

        public static class AdminDivisionPicker {
            public const string Placeholder = "ui.admin_division_picker.placeholder";
            public const string ClearAriaLabel = "ui.admin_division_picker.clear_aria_label";
        }

        public static class ChipsInput {
            public const string RemoveAriaLabel = "ui.chips_input.remove_aria_label";
            public const string ExternalCodePlaceholder = "ui.chips_input.external_code_placeholder";
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

        public static class Settings {
            public const string PageTitle = "ui.settings.page_title";
            public const string Intro = "ui.settings.intro";
            public const string AdvancedLink = "ui.settings.advanced_link";
            public const string NotFound = "ui.settings.not_found";
            public const string GroupBasics = "ui.settings.group.basics";
            public const string GroupLogin = "ui.settings.group.login";
            public const string GroupCommunication = "ui.settings.group.communication";
            public const string GroupIntegrations = "ui.settings.group.integrations";
            public const string GroupAssembly = "ui.settings.group.assembly";
            public const string StatusConfigured = "ui.settings.status.configured";
            public const string StatusPartial = "ui.settings.status.partial";
            public const string StatusNotConfigured = "ui.settings.status.not_configured";
            public const string StatusOn = "ui.settings.status.on";
            public const string StatusOff = "ui.settings.status.off";
            public const string CheckButton = "ui.settings.check_button";
            public const string UseCurrentAddress = "ui.settings.use_current_address";
            public const string OverridesLink = "ui.settings.overrides_link";
            public const string DurationInvalid = "ui.settings.duration_invalid";
            public const string FilledFromCheck = "ui.settings.filled_from_check";
            public const string UnitSeconds = "ui.settings.unit.seconds";
            public const string UnitMinutes = "ui.settings.unit.minutes";
            public const string UnitHours = "ui.settings.unit.hours";
            public const string UnitDays = "ui.settings.unit.days";
            public const string TravelExpensesTitle = "ui.settings.travel_expenses.title";
            public const string TravelExpensesDescription = "ui.settings.travel_expenses.description";
            public const string TravelExpensesReceiptsTitle = "ui.settings.travel_expenses.receipts_title";
            public const string TravelExpensesReceiptsDescription = "ui.settings.travel_expenses.receipts_description";
            public const string UnitMegabytes = "ui.settings.unit.megabytes";
            public const string LinkTravelExpenseRules = "ui.settings.link.travel_expense_rules";
            public const string UnitMinutesSeconds = "ui.settings.unit.minutes_seconds";
            public const string LinkImportHistory = "ui.settings.link.import_history";
            public const string LinkNewsFeeds = "ui.settings.link.news_feeds";
            public const string GeneralTitle = "ui.settings.general.title";
            public const string GeneralDescription = "ui.settings.general.description";
            public const string PublicFormsTitle = "ui.settings.public_forms.title";
            public const string PublicFormsDescription = "ui.settings.public_forms.description";
            public const string SamlTitle = "ui.settings.saml.title";
            public const string SamlDescription = "ui.settings.saml.description";
            public const string OidcTitle = "ui.settings.oidc.title";
            public const string OidcDescription = "ui.settings.oidc.description";
            public const string SecurityTitle = "ui.settings.security.title";
            public const string SecurityDescription = "ui.settings.security.description";
            public const string SecuritySessionsTitle = "ui.settings.security.sessions_title";
            public const string SecurityLockoutTitle = "ui.settings.security.lockout_title";
            public const string SecurityRateLimitTitle = "ui.settings.security.rate_limit_title";
            public const string SmtpTitle = "ui.settings.smtp.title";
            public const string SmtpDescription = "ui.settings.smtp.description";
            public const string ImapTitle = "ui.settings.imap.title";
            public const string ImapDescription = "ui.settings.imap.description";
            public const string TelegramTitle = "ui.settings.telegram.title";
            public const string TelegramDescription = "ui.settings.telegram.description";
            public const string MemberImportTitle = "ui.settings.member_import.title";
            public const string MemberImportDescription = "ui.settings.member_import.description";
            public const string KeycloakTitle = "ui.settings.keycloak.title";
            public const string KeycloakDescription = "ui.settings.keycloak.description";
            public const string WikiTitle = "ui.settings.wiki.title";
            public const string WikiDescription = "ui.settings.wiki.description";
            public const string OpenSlidesTitle = "ui.settings.openslides.title";
            public const string OpenSlidesDescription = "ui.settings.openslides.description";
            public const string SocialMediaTitle = "ui.settings.social_media.title";
            public const string SocialMediaDescription = "ui.settings.social_media.description";
            public const string SocialMediaMistralTitle = "ui.settings.social_media.mistral_title";
            public const string SocialMediaMistralDescription = "ui.settings.social_media.mistral_description";
            public const string SocialMediaPostizTitle = "ui.settings.social_media.postiz_title";
            public const string SocialMediaPipelineTitle = "ui.settings.social_media.pipeline_title";
            public const string AssemblyAssistantTitle = "ui.settings.assembly_assistant.title";
            public const string AssemblyAssistantDescription = "ui.settings.assembly_assistant.description";
            public const string AssemblyProcedureTitle = "ui.settings.assembly_procedure.title";
            public const string AssemblyProcedureDescription = "ui.settings.assembly_procedure.description";
            public const string AssemblyTimesTitle = "ui.settings.assembly_procedure.times_title";
            public const string AssemblySpeakersTitle = "ui.settings.assembly_procedure.speakers_title";
            public const string AssemblyPollsTitle = "ui.settings.assembly_procedure.polls_title";
            public const string XReplyEveryone = "ui.settings.x_reply.everyone";
            public const string XReplyFollowing = "ui.settings.x_reply.following";
            public const string XReplyMentioned = "ui.settings.x_reply.mentioned";
            public const string XReplySubscribers = "ui.settings.x_reply.subscribers";
            public const string XReplyVerified = "ui.settings.x_reply.verified";
        }

        public static class SettingsCheck {
            public const string NotConfigured = "ui.settings_check.not_configured";
            public const string InsecureUrl = "ui.settings_check.insecure_url";
            public const string Failed = "ui.settings_check.failed";
            public const string TelegramOk = "ui.settings_check.telegram_ok";
            public const string MemberImportOk = "ui.settings_check.member_import_ok";
            public const string MemberImportFileMissing = "ui.settings_check.member_import_file_missing";
            public const string MemberImportUnreadable = "ui.settings_check.member_import_unreadable";
            public const string MemberImportColumnMissing = "ui.settings_check.member_import_column_missing";
            public const string KeycloakOk = "ui.settings_check.keycloak_ok";
            public const string LoginOk = "ui.settings_check.login_ok";
            public const string MistralOk = "ui.settings_check.mistral_ok";
            public const string PostizOk = "ui.settings_check.postiz_ok";
        }

        public static class Receipts {
            public const string PageTitle = "ui.receipts.page_title";
            public const string PageIntro = "ui.receipts.page_intro";
            public const string UploadNew = "ui.receipts.upload_new";
            public const string Usage = "ui.receipts.usage";
            public const string UsageUnlimited = "ui.receipts.usage_unlimited";
            public const string Empty = "ui.receipts.empty";
            public const string LabelColumn = "ui.receipts.label_column";
            public const string FileColumn = "ui.receipts.file_column";
            public const string UploadedColumn = "ui.receipts.uploaded_column";
            public const string AttachedLabel = "ui.receipts.attached_label";
            public const string Choose = "ui.receipts.choose";
            public const string Detach = "ui.receipts.detach";
            public const string NoneAvailable = "ui.receipts.none_available";
            public const string Missing = "ui.receipts.missing";
            public const string AnonymousHint = "ui.receipts.anonymous_hint";
        }

        public static class TravelExpenseRules {
            public const string PageTitle = "ui.travel_expense_rules.page_title";
            public const string PageIntro = "ui.travel_expense_rules.page_intro";
            public const string NoPermission = "ui.travel_expense_rules.no_permission";
            public const string ScopeGlobal = "ui.travel_expense_rules.scope_global";
            public const string ScopeChapter = "ui.travel_expense_rules.scope_chapter";
            public const string ValidFromShort = "ui.travel_expense_rules.valid_from_short";
            public const string NoVersions = "ui.travel_expense_rules.no_versions";
            public const string InheritsHint = "ui.travel_expense_rules.inherits_hint";
            public const string NewVersion = "ui.travel_expense_rules.new_version";
            public const string Unsaved = "ui.travel_expense_rules.unsaved";
            public const string NewFromNewest = "ui.travel_expense_rules.new_from_newest";
            public const string NewFromPreset = "ui.travel_expense_rules.new_from_preset";
            public const string EditWarning = "ui.travel_expense_rules.edit_warning";
            public const string Saved = "ui.travel_expense_rules.saved";
            public const string VersionLabel = "ui.travel_expense_rules.version_label";
            public const string ValidFrom = "ui.travel_expense_rules.valid_from";
            public const string TemplateKey = "ui.travel_expense_rules.template_key";
            public const string TemplateKeyHint = "ui.travel_expense_rules.template_key_hint";
            public const string HeaderLines = "ui.travel_expense_rules.header_lines";
            public const string HeaderLinesHint = "ui.travel_expense_rules.header_lines_hint";
            public const string CarRates = "ui.travel_expense_rules.car_rates";
            public const string MotorcycleRates = "ui.travel_expense_rules.motorcycle_rates";
            public const string StepUpToKm = "ui.travel_expense_rules.step_up_to_km";
            public const string StepRate = "ui.travel_expense_rules.step_rate";
            public const string StepOpenEnded = "ui.travel_expense_rules.step_open_ended";
            public const string StepAdd = "ui.travel_expense_rules.step_add";
            public const string MealsTitle = "ui.travel_expense_rules.meals_title";
            public const string BreakfastReduction = "ui.travel_expense_rules.breakfast_reduction";
            public const string LunchReduction = "ui.travel_expense_rules.lunch_reduction";
            public const string DinnerReduction = "ui.travel_expense_rules.dinner_reduction";
            public const string ReductionHint = "ui.travel_expense_rules.reduction_hint";
            public const string AccommodationFlatRate = "ui.travel_expense_rules.accommodation_flat_rate";
            public const string DeadlineDays = "ui.travel_expense_rules.deadline_days";
            public const string Days = "ui.travel_expense_rules.days";
            public const string Cutoff = "ui.travel_expense_rules.cutoff";
            public const string CutoffDay = "ui.travel_expense_rules.cutoff_day";
            public const string CutoffMonth = "ui.travel_expense_rules.cutoff_month";
            public const string CutoffHint = "ui.travel_expense_rules.cutoff_hint";
            public const string RulesText = "ui.travel_expense_rules.rules_text";
            public const string RulesTextHint = "ui.travel_expense_rules.rules_text_hint";
        }

        public static class TravelExpense {
            public const string PageTitle = "ui.travel_expense.page_title";
            public const string StepPerson = "ui.travel_expense.step_person";
            public const string StepChapter = "ui.travel_expense.step_chapter";
            public const string StepTrip = "ui.travel_expense.step_trip";
            public const string StepBuilder = "ui.travel_expense.step_builder";
            public const string StepPayout = "ui.travel_expense.step_payout";
            public const string StepOverview = "ui.travel_expense.step_overview";
            public const string PersonRequired = "ui.travel_expense.person_required";
            public const string TimesRequired = "ui.travel_expense.times_required";
            public const string MemberNumberLabel = "ui.travel_expense.member_number_label";
            public const string MemberNumberHint = "ui.travel_expense.member_number_hint";
            public const string EmailLabel = "ui.travel_expense.email_label";
            public const string StreetLabel = "ui.travel_expense.street_label";
            public const string PostCodeLabel = "ui.travel_expense.post_code_label";
            public const string CityLabel = "ui.travel_expense.city_label";
            public const string MembershipChapterLabel = "ui.travel_expense.membership_chapter_label";
            public const string TravelChapterLabel = "ui.travel_expense.travel_chapter_label";
            public const string TravelChapterHint = "ui.travel_expense.travel_chapter_hint";
            public const string FunctionLabel = "ui.travel_expense.function_label";
            public const string FunctionSuggestions = "ui.travel_expense.function_suggestions";
            public const string RulesTitle = "ui.travel_expense.rules_title";
            public const string RulesVersion = "ui.travel_expense.rules_version";
            public const string RulesCar = "ui.travel_expense.rules_car";
            public const string RulesMotorcycle = "ui.travel_expense.rules_motorcycle";
            public const string RulesNoMotorcycle = "ui.travel_expense.rules_no_motorcycle";
            public const string RulesMeals = "ui.travel_expense.rules_meals";
            public const string RulesAccommodation = "ui.travel_expense.rules_accommodation";
            public const string RulesDeadline = "ui.travel_expense.rules_deadline";
            public const string PurposeLabel = "ui.travel_expense.purpose_label";
            public const string PurposeHint = "ui.travel_expense.purpose_hint";
            public const string DestinationLabel = "ui.travel_expense.destination_label";
            public const string ResolutionNumberLabel = "ui.travel_expense.resolution_number_label";
            public const string ResolutionDateLabel = "ui.travel_expense.resolution_date_label";
            public const string ResolutionHint = "ui.travel_expense.resolution_hint";
            public const string AbsenceHint = "ui.travel_expense.absence_hint";
            public const string StartTitle = "ui.travel_expense.start_title";
            public const string EndTitle = "ui.travel_expense.end_title";
            public const string PlaceLabel = "ui.travel_expense.place_label";
            public const string StartAtLabel = "ui.travel_expense.start_at_label";
            public const string EndAtLabel = "ui.travel_expense.end_at_label";
            public const string Empty = "ui.travel_expense.empty";
            public const string AddLabel = "ui.travel_expense.add_label";
            public const string InsertHere = "ui.travel_expense.insert_here";
            public const string AddVehicle = "ui.travel_expense.add_vehicle";
            public const string AddTransit = "ui.travel_expense.add_transit";
            public const string AddHotel = "ui.travel_expense.add_hotel";
            public const string AddMeal = "ui.travel_expense.add_meal";
            public const string AddOther = "ui.travel_expense.add_other";
            public const string Remove = "ui.travel_expense.remove";
            public const string VehicleCar = "ui.travel_expense.vehicle_car";
            public const string VehicleMotorcycle = "ui.travel_expense.vehicle_motorcycle";
            public const string TransitTrain = "ui.travel_expense.transit_train";
            public const string TransitBus = "ui.travel_expense.transit_bus";
            public const string TransitPlane = "ui.travel_expense.transit_plane";
            public const string TransitOther = "ui.travel_expense.transit_other";
            public const string FromLabel = "ui.travel_expense.from_label";
            public const string ToLabel = "ui.travel_expense.to_label";
            public const string DepartureLabel = "ui.travel_expense.departure_label";
            public const string ArrivalLabel = "ui.travel_expense.arrival_label";
            public const string KmLabel = "ui.travel_expense.km_label";
            public const string KmHint = "ui.travel_expense.km_hint";
            public const string TicketLabel = "ui.travel_expense.ticket_label";
            public const string DescriptionLabel = "ui.travel_expense.description_label";
            public const string CheckInLabel = "ui.travel_expense.check_in_label";
            public const string CheckOutLabel = "ui.travel_expense.check_out_label";
            public const string HotelReceiptOption = "ui.travel_expense.hotel_receipt_option";
            public const string HotelFlatOption = "ui.travel_expense.hotel_flat_option";
            public const string HotelAmountLabel = "ui.travel_expense.hotel_amount_label";
            public const string BreakfastIncludedLabel = "ui.travel_expense.breakfast_included_label";
            public const string BreakfastAmountLabel = "ui.travel_expense.breakfast_amount_label";
            public const string BreakfastAmountHint = "ui.travel_expense.breakfast_amount_hint";
            public const string MealHint = "ui.travel_expense.meal_hint";
            public const string MealDayLabel = "ui.travel_expense.meal_day_label";
            public const string MealBreakfast = "ui.travel_expense.meal_breakfast";
            public const string MealLunch = "ui.travel_expense.meal_lunch";
            public const string MealDinner = "ui.travel_expense.meal_dinner";
            public const string DateLabel = "ui.travel_expense.date_label";
            public const string AmountLabel = "ui.travel_expense.amount_label";
            public const string TotalsTitle = "ui.travel_expense.totals_title";
            public const string TotalCar = "ui.travel_expense.total_car";
            public const string TotalMotorcycle = "ui.travel_expense.total_motorcycle";
            public const string TotalTickets = "ui.travel_expense.total_tickets";
            public const string TotalMeals = "ui.travel_expense.total_meals";
            public const string TotalAccommodation = "ui.travel_expense.total_accommodation";
            public const string TotalOther = "ui.travel_expense.total_other";
            public const string TotalSum = "ui.travel_expense.total_sum";
            public const string TotalDonation = "ui.travel_expense.total_donation";
            public const string TotalPayout = "ui.travel_expense.total_payout";
            public const string DonationTitle = "ui.travel_expense.donation_title";
            public const string DonationHint = "ui.travel_expense.donation_hint";
            public const string DonationNone = "ui.travel_expense.donation_none";
            public const string DonationAll = "ui.travel_expense.donation_all";
            public const string DonationParts = "ui.travel_expense.donation_parts";
            public const string DonationAmount = "ui.travel_expense.donation_amount";
            public const string DonateTravel = "ui.travel_expense.donate_travel";
            public const string DonateMeals = "ui.travel_expense.donate_meals";
            public const string DonateAccommodation = "ui.travel_expense.donate_accommodation";
            public const string DonateOther = "ui.travel_expense.donate_other";
            public const string BankTitle = "ui.travel_expense.bank_title";
            public const string BicLabel = "ui.travel_expense.bic_label";
            public const string AttachmentsTitle = "ui.travel_expense.attachments_title";
            public const string PaperAttachmentsLabel = "ui.travel_expense.paper_attachments_label";
            public const string PaperAttachmentsHint = "ui.travel_expense.paper_attachments_hint";
            public const string PaperAttachmentsHintAnonymous = "ui.travel_expense.paper_attachments_hint_anonymous";
            public const string FormDateLabel = "ui.travel_expense.form_date_label";
            public const string NoChapter = "ui.travel_expense.no_chapter";
            public const string NoMembershipChapter = "ui.travel_expense.no_membership_chapter";
            public const string TimelineTitle = "ui.travel_expense.timeline_title";
            public const string MealDaysTitle = "ui.travel_expense.meal_days_title";
            public const string MealColumnAllowance = "ui.travel_expense.meal_column_allowance";
            public const string MealColumnReduction = "ui.travel_expense.meal_column_reduction";
            public const string MealColumnNet = "ui.travel_expense.meal_column_net";
            public const string DownloadButton = "ui.travel_expense.download_button";
            public const string DownloadAgain = "ui.travel_expense.download_again";
            public const string CopyToDraft = "ui.travel_expense.copy_to_draft";
            public const string DeleteDraft = "ui.travel_expense.delete_draft";
            public const string SavedAt = "ui.travel_expense.saved_at";
            public const string SubmittedNotice = "ui.travel_expense.submitted_notice";

            public static class Module {
                public const string VehicleCar = "ui.travel_expense.module.vehicle_car";
                public const string VehicleMotorcycle = "ui.travel_expense.module.vehicle_motorcycle";
                public const string TransitTrain = "ui.travel_expense.module.transit_train";
                public const string TransitBus = "ui.travel_expense.module.transit_bus";
                public const string TransitPlane = "ui.travel_expense.module.transit_plane";
                public const string TransitOther = "ui.travel_expense.module.transit_other";
                public const string HotelStay = "ui.travel_expense.module.hotel_stay";
                public const string MealBreakfast = "ui.travel_expense.module.meal_breakfast";
                public const string MealLunch = "ui.travel_expense.module.meal_lunch";
                public const string MealDinner = "ui.travel_expense.module.meal_dinner";
                public const string OtherCost = "ui.travel_expense.module.other_cost";
                public const string Start = "ui.travel_expense.module.start";
                public const string End = "ui.travel_expense.module.end";
            }

            public static class MealDay {
                public const string DayTrip = "ui.travel_expense.meal_day.day_trip";
                public const string ArrivalDeparture = "ui.travel_expense.meal_day.arrival_departure";
                public const string FullDay = "ui.travel_expense.meal_day.full_day";
                public const string None = "ui.travel_expense.meal_day.none";
            }

            public static class Severity {
                public const string Problem = "ui.travel_expense.severity.problem";
                public const string Warning = "ui.travel_expense.severity.warning";
                public const string Info = "ui.travel_expense.severity.info";
            }

            public static class Pdf {
                public const string MoreOtherCosts = "ui.travel_expense.pdf.more_other_costs";
                public const string RateUpTo = "ui.travel_expense.pdf.rate_up_to";
                public const string RateThen = "ui.travel_expense.pdf.rate_then";
                public const string HotelReceiptNights = "ui.travel_expense.pdf.hotel_receipt_nights";
                public const string HotelFlatNights = "ui.travel_expense.pdf.hotel_flat_nights";
                public const string BreakfastDeducted = "ui.travel_expense.pdf.breakfast_deducted";
                public const string OneNight = "ui.travel_expense.pdf.one_night";
                public const string Nights = "ui.travel_expense.pdf.nights";
                public const string AttachmentLabel = "ui.travel_expense.pdf.attachment_label";
                public const string ReceiptMissing = "ui.travel_expense.pdf.receipt_missing";
                public const string ReceiptNotMergeable = "ui.travel_expense.pdf.receipt_not_mergeable";
            }

            public static class Note {
                public const string NameMissing = "ui.travel_expense.note.name_missing";
                public const string AddressIncomplete = "ui.travel_expense.note.address_incomplete";
                public const string MemberNumberMissing = "ui.travel_expense.note.member_number_missing";
                public const string IbanMissing = "ui.travel_expense.note.iban_missing";
                public const string IbanInvalid = "ui.travel_expense.note.iban_invalid";
                public const string PurposeMissing = "ui.travel_expense.note.purpose_missing";
                public const string ResolutionMissing = "ui.travel_expense.note.resolution_missing";
                public const string NoCosts = "ui.travel_expense.note.no_costs";
                public const string TimesMissing = "ui.travel_expense.note.times_missing";
                public const string EndBeforeStart = "ui.travel_expense.note.end_before_start";
                public const string DeadlinePassed = "ui.travel_expense.note.deadline_passed";
                public const string CutoffPassed = "ui.travel_expense.note.cutoff_passed";
                public const string MotorcycleNotReimbursed = "ui.travel_expense.note.motorcycle_not_reimbursed";
                public const string VehicleLegWithoutKm = "ui.travel_expense.note.vehicle_leg_without_km";
                public const string AmountMissing = "ui.travel_expense.note.amount_missing";
                public const string ModuleOutsideTrip = "ui.travel_expense.note.module_outside_trip";
                public const string LegsOverlap = "ui.travel_expense.note.legs_overlap";
            }
        }

        public static class PublicFormWizard {
            public const string MembershipApplicationTitle = "ui.public_form_wizard.membership_application_title";
            public const string DueSelectionTitle = "ui.public_form_wizard.due_selection_title";
            public const string StepPersonalData = "ui.public_form_wizard.step.personal_data";
            public const string StepAddress = "ui.public_form_wizard.step.address";
            public const string StepDues = "ui.public_form_wizard.step.dues";
            public const string StepPayment = "ui.public_form_wizard.step.payment";
            public const string StepDeclarations = "ui.public_form_wizard.step.declarations";
            public const string StepSummary = "ui.public_form_wizard.step.summary";
            public const string PersonalDataRequired = "ui.public_form_wizard.personal_data_required";
            public const string AddressRequired = "ui.public_form_wizard.address_required";
            public const string DuesRequired = "ui.public_form_wizard.dues_required";
            public const string PaymentRequired = "ui.public_form_wizard.payment_required";
            public const string DeclarationsRequired = "ui.public_form_wizard.declarations_required";
            public const string SearchMunicipalityAgain = "ui.public_form_wizard.search_municipality_again";
        }

        public static class PublicFormsSetup {
            public const string Intro = "ui.public_forms_setup.intro";
            public const string MembershipTitle = "ui.public_forms_setup.membership_title";
            public const string MembershipLabel = "ui.public_forms_setup.membership_label";
            public const string DueSelectionLabel = "ui.public_forms_setup.due_selection_label";
            public const string MembershipHint = "ui.public_forms_setup.membership_hint";
            public const string MotionTitle = "ui.public_forms_setup.motion_title";
            public const string MotionDescription = "ui.public_forms_setup.motion_description";
            public const string MotionDefaultLabel = "ui.public_forms_setup.motion_default_label";
            public const string OverridesTitle = "ui.public_forms_setup.overrides_title";
            public const string NoOverrides = "ui.public_forms_setup.no_overrides";
            public const string ValueColumn = "ui.public_forms_setup.value_column";
            public const string Open = "ui.public_forms_setup.open";
            public const string Closed = "ui.public_forms_setup.closed";
            public const string Inherit = "ui.public_forms_setup.inherit";
            public const string InheritHint = "ui.public_forms_setup.inherit_hint";
            public const string AddChapterPlaceholder = "ui.public_forms_setup.add_chapter_placeholder";
            public const string AddButton = "ui.public_forms_setup.add_button";
            public const string OpenCount = "ui.public_forms_setup.open_count";
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

        public static class FormWizard {
            public const string BackButton = "ui.form_wizard.back_button";
            public const string NextButton = "ui.form_wizard.next_button";
            public const string SubmitButton = "ui.form_wizard.submit_button";
            public const string SaveDraftButton = "ui.form_wizard.save_draft_button";
        }

        public static class FormCatalog {
            public const string PageTitle = "ui.form_catalog.page_title";
            public const string PageIntro = "ui.form_catalog.page_intro";
            public const string StartButton = "ui.form_catalog.start_button";
            public const string MyFormsLink = "ui.form_catalog.my_forms_link";
            public const string ExpenseReimbursementDescription = "ui.form_catalog.expense_reimbursement_description";
            public const string TravelExpenseDescription = "ui.form_catalog.travel_expense_description";
        }

        public static class MyForms {
            public const string PageTitle = "ui.my_forms.page_title";
            public const string PageIntroAuth = "ui.my_forms.page_intro_auth";
            public const string PageIntroAnonymous = "ui.my_forms.page_intro_anonymous";
            public const string LocalDraftsSection = "ui.my_forms.local_drafts_section";
            public const string ServerDraftsSection = "ui.my_forms.server_drafts_section";
            public const string LocalDraftsHint = "ui.my_forms.local_drafts_hint";
            public const string EmptyState = "ui.my_forms.empty_state";
            public const string EmptyStateLocal = "ui.my_forms.empty_state_local";
            public const string FormColumn = "ui.my_forms.form_column";
            public const string StatusColumn = "ui.my_forms.status_column";
            public const string ChapterColumn = "ui.my_forms.chapter_column";
            public const string TotalColumn = "ui.my_forms.total_column";
            public const string UpdatedColumn = "ui.my_forms.updated_column";
            public const string SavedAtColumn = "ui.my_forms.saved_at_column";
            public const string ActionsColumn = "ui.my_forms.actions_column";
            public const string StatusDraft = "ui.my_forms.status_draft";
            public const string StatusSubmitted = "ui.my_forms.status_submitted";
            public const string StatusLocal = "ui.my_forms.status_local";
            public const string OpenAction = "ui.my_forms.open_action";
            public const string DownloadAction = "ui.my_forms.download_action";
            public const string CopyAction = "ui.my_forms.copy_action";
            public const string DeleteAction = "ui.my_forms.delete_action";
        }

        public static class AnonymousFormGate {
            public const string Title = "ui.anonymous_form_gate.title";
            public const string IntroParagraph = "ui.anonymous_form_gate.intro_paragraph";
            public const string PointAnonymous = "ui.anonymous_form_gate.point_anonymous";
            public const string PointLogin = "ui.anonymous_form_gate.point_login";
            public const string PointLocalStorage = "ui.anonymous_form_gate.point_local_storage";
            public const string CancelButton = "ui.anonymous_form_gate.cancel_button";
            public const string AcceptInMemoryButton = "ui.anonymous_form_gate.accept_in_memory_button";
            public const string AcceptWithStorageButton = "ui.anonymous_form_gate.accept_with_storage_button";
        }

        public static class ExpenseReimbursement {
            public const string PageTitle = "ui.expense_reimbursement.page_title";
            public const string Step1Title = "ui.expense_reimbursement.step1_title";
            public const string Step2Title = "ui.expense_reimbursement.step2_title";
            public const string Step3Title = "ui.expense_reimbursement.step3_title";
            public const string Step4Title = "ui.expense_reimbursement.step4_title";
            public const string Step5Title = "ui.expense_reimbursement.step5_title";

            public const string FirstNameLabel = "ui.expense_reimbursement.first_name_label";
            public const string LastNameLabel = "ui.expense_reimbursement.last_name_label";
            public const string MemberNumberLabel = "ui.expense_reimbursement.member_number_label";
            public const string StreetLabel = "ui.expense_reimbursement.street_label";
            public const string PostCodeLabel = "ui.expense_reimbursement.post_code_label";
            public const string CityLabel = "ui.expense_reimbursement.city_label";

            public const string ChapterLabel = "ui.expense_reimbursement.chapter_label";
            public const string FunctionLabel = "ui.expense_reimbursement.function_label";
            public const string FunctionPlaceholder = "ui.expense_reimbursement.function_placeholder";
            public const string MotionDateLabel = "ui.expense_reimbursement.motion_date_label";

            public const string LineItemDescription = "ui.expense_reimbursement.line_item_description";
            public const string LineItemValue = "ui.expense_reimbursement.line_item_value";
            public const string LineItemAdd = "ui.expense_reimbursement.line_item_add";
            public const string LineItemSum = "ui.expense_reimbursement.line_item_sum";

            public const string DonatedAmountLabel = "ui.expense_reimbursement.donated_amount_label";
            public const string PayoutAmountLabel = "ui.expense_reimbursement.payout_amount_label";
            public const string SplitMismatch = "ui.expense_reimbursement.split_mismatch";
            public const string SplitOk = "ui.expense_reimbursement.split_ok";
            public const string RequestDonationReceipt = "ui.expense_reimbursement.request_donation_receipt";
            public const string PayoutMethodSection = "ui.expense_reimbursement.payout_method_section";
            public const string PayoutCash = "ui.expense_reimbursement.payout_cash";
            public const string PayoutBankTransfer = "ui.expense_reimbursement.payout_bank_transfer";
            public const string IbanLabel = "ui.expense_reimbursement.iban_label";
            public const string BicHint = "ui.expense_reimbursement.bic_hint";
            public const string SignatureLocationLabel = "ui.expense_reimbursement.signature_location_label";
            public const string FormDateLabel = "ui.expense_reimbursement.form_date_label";

            public const string OverviewIntro = "ui.expense_reimbursement.overview_intro";
            public const string GeneratePdfButton = "ui.expense_reimbursement.generate_pdf_button";
            public const string DownloadAgainButton = "ui.expense_reimbursement.download_again_button";
            public const string CopyToDraftButton = "ui.expense_reimbursement.copy_to_draft_button";
            public const string DeleteDraftButton = "ui.expense_reimbursement.delete_draft_button";
            public const string SubmittedNotice = "ui.expense_reimbursement.submitted_notice";

            public const string ValidationPersonalRequired = "ui.expense_reimbursement.validation_personal_required";
            public const string ValidationChapterRequired = "ui.expense_reimbursement.validation_chapter_required";
            public const string ValidationLineItemsRequired = "ui.expense_reimbursement.validation_line_items_required";
            public const string ValidationSplitMismatch = "ui.expense_reimbursement.validation_split_mismatch";
            public const string ValidationIbanRequired = "ui.expense_reimbursement.validation_iban_required";
        }

        public static class Accreditation {
            public const string Check = "ui.accreditation.check";
            public const string CheckPassed = "ui.accreditation.check_passed";
            public const string Suggestion = "ui.accreditation.suggestion";
            public const string AcceptSuggestion = "ui.accreditation.accept_suggestion";
            public const string PageTitle = "ui.accreditation.page_title";
            public const string NotConfigured = "ui.accreditation.not_configured";
            public const string EligibilityTitle = "ui.accreditation.eligibility_title";
            public const string EligibilityIntro = "ui.accreditation.eligibility_intro";
            public const string MemberNumber = "ui.accreditation.member_number";
            public const string NoAccount = "ui.accreditation.no_account";
            public const string MultipleAccounts = "ui.accreditation.multiple_accounts";
            public const string Username = "ui.accreditation.username";
            public const string Name = "ui.accreditation.name";
            public const string Email = "ui.accreditation.email";
            public const string Status = "ui.accreditation.status";
            public const string StatusActive = "ui.accreditation.status_active";
            public const string StatusDisabled = "ui.accreditation.status_disabled";
            public const string StatusClosed = "ui.accreditation.status_closed";
            public const string Eligible = "ui.accreditation.eligible";
            public const string Verified = "ui.accreditation.verified";
            public const string Yes = "ui.accreditation.yes";
            public const string No = "ui.accreditation.no";
            public const string AlreadyEligible = "ui.accreditation.already_eligible";
            public const string DisabledWarning = "ui.accreditation.disabled_warning";
            public const string SetEligible = "ui.accreditation.set_eligible";
            public const string SetEligibleConfirm = "ui.accreditation.set_eligible_confirm";
            public const string SetEligibleDone = "ui.accreditation.set_eligible_done";
            public const string TemporaryTitle = "ui.accreditation.temporary_title";
            public const string TemporaryIntro = "ui.accreditation.temporary_intro";
            public const string FirstName = "ui.accreditation.first_name";
            public const string LastName = "ui.accreditation.last_name";
            public const string UsernameHint = "ui.accreditation.username_hint";
            public const string EmailOptional = "ui.accreditation.email_optional";
            public const string Create = "ui.accreditation.create";
            public const string CreatedTitle = "ui.accreditation.created_title";
            public const string CreatedWarningTitle = "ui.accreditation.created_warning_title";
            public const string Password = "ui.accreditation.password";
            public const string Copy = "ui.accreditation.copy";
            public const string Copied = "ui.accreditation.copied";
            public const string CreatedNote = "ui.accreditation.created_note";
            public const string ExistingAccount = "ui.accreditation.existing_account";
        }

        public static class AssemblyMotion {
            public const string AddType = "ui.assembly_motion.add_type";
            public const string AuthorMarker = "ui.assembly_motion.author_marker";
            public const string BerlinTimeHint = "ui.assembly_motion.berlin_time_hint";
            public const string CheckNote = "ui.assembly_motion.check_note";
            public const string ClosesAt = "ui.assembly_motion.closes_at";
            public const string CollectingButton = "ui.assembly_motion.collecting_button";
            public const string CollectingDeadline = "ui.assembly_motion.collecting_deadline";
            public const string CollectingPageTitle = "ui.assembly_motion.collecting_page_title";
            public const string CollectingProgress = "ui.assembly_motion.collecting_progress";
            public const string ColumnCode = "ui.assembly_motion.column_code";
            public const string ColumnCompetingDeadline = "ui.assembly_motion.column_competing_deadline";
            public const string ColumnDeadline = "ui.assembly_motion.column_deadline";
            public const string ColumnInquiries = "ui.assembly_motion.column_inquiries";
            public const string ColumnMajority = "ui.assembly_motion.column_majority";
            public const string ColumnMinSubmitters = "ui.assembly_motion.column_min_submitters";
            public const string ColumnNumber = "ui.assembly_motion.column_number";
            public const string ColumnRole = "ui.assembly_motion.column_role";
            public const string ColumnSortOrder = "ui.assembly_motion.column_sort_order";
            public const string ColumnType = "ui.assembly_motion.column_type";
            public const string Competitions = "ui.assembly_motion.competitions";
            public const string CopyLink = "ui.assembly_motion.copy_link";
            public const string DatesRequired = "ui.assembly_motion.dates_required";
            public const string DeadlinesTitle = "ui.assembly_motion.deadlines_title";
            public const string DeleteConfirmMessage = "ui.assembly_motion.delete_confirm_message";
            public const string DeleteConfirmTitle = "ui.assembly_motion.delete_confirm_title";
            public const string DeleteDraft = "ui.assembly_motion.delete_draft";
            public const string DeleteTypeMessage = "ui.assembly_motion.delete_type_message";
            public const string DeleteTypeTitle = "ui.assembly_motion.delete_type_title";
            public const string DetailPageTitle = "ui.assembly_motion.detail_page_title";
            public const string EditMotionTitle = "ui.assembly_motion.edit_motion_title";
            public const string EditNotAllowed = "ui.assembly_motion.edit_not_allowed";
            public const string EditorPageTitle = "ui.assembly_motion.editor_page_title";
            public const string EnablePortal = "ui.assembly_motion.enable_portal";
            public const string FieldKeywords = "ui.assembly_motion.field_keywords";
            public const string FieldMajority = "ui.assembly_motion.field_majority";
            public const string FieldPriorDiscussion = "ui.assembly_motion.field_prior_discussion";
            public const string FieldReason = "ui.assembly_motion.field_reason";
            public const string FieldSection = "ui.assembly_motion.field_section";
            public const string FieldSummary = "ui.assembly_motion.field_summary";
            public const string FieldText = "ui.assembly_motion.field_text";
            public const string FieldType = "ui.assembly_motion.field_type";
            public const string FilterAllStatuses = "ui.assembly_motion.filter_all_statuses";
            public const string FilterAllTypes = "ui.assembly_motion.filter_all_types";
            public const string FormattingHelp = "ui.assembly_motion.formatting_help";
            public const string Intro = "ui.assembly_motion.intro";
            public const string Join = "ui.assembly_motion.join";
            public const string JoinConfirmMessage = "ui.assembly_motion.join_confirm_message";
            public const string JoinConfirmTitle = "ui.assembly_motion.join_confirm_title";
            public const string KeywordsHelp = "ui.assembly_motion.keywords_help";
            public const string Leave = "ui.assembly_motion.leave";
            public const string LeaveConfirmMessage = "ui.assembly_motion.leave_confirm_message";
            public const string LeaveConfirmTitle = "ui.assembly_motion.leave_confirm_title";
            public const string MajoritySimple = "ui.assembly_motion.majority_simple";
            public const string MajorityTwoThirds = "ui.assembly_motion.majority_two_thirds";
            public const string MembersOnly = "ui.assembly_motion.members_only";
            public const string MineButton = "ui.assembly_motion.mine_button";
            public const string MinePageTitle = "ui.assembly_motion.mine_page_title";
            public const string MotionNotFound = "ui.assembly_motion.motion_not_found";
            public const string NewMotion = "ui.assembly_motion.new_motion";
            public const string NoCollecting = "ui.assembly_motion.no_collecting";
            public const string NoMotions = "ui.assembly_motion.no_motions";
            public const string NoOwnMotions = "ui.assembly_motion.no_own_motions";
            public const string NotEligibleTitle = "ui.assembly_motion.not_eligible_title";
            public const string OpenPortal = "ui.assembly_motion.open_portal";
            public const string OpensAt = "ui.assembly_motion.opens_at";
            public const string OpenSlidesMeetingId = "ui.assembly_motion.open_slides_meeting_id";
            public const string PortalNotFound = "ui.assembly_motion.portal_not_found";
            public const string PortalPageTitle = "ui.assembly_motion.portal_page_title";
            public const string PortalWindow = "ui.assembly_motion.portal_window";
            public const string PortalWindowTo = "ui.assembly_motion.portal_window_to";
            public const string PrioritizationEnabled = "ui.assembly_motion.prioritization_enabled";
            public const string PrioritizationEndsAt = "ui.assembly_motion.prioritization_ends_at";
            public const string PrioritizationStartsAt = "ui.assembly_motion.prioritization_starts_at";
            public const string ProblemAnonymized = "ui.assembly_motion.problem_anonymized";
            public const string ProblemExited = "ui.assembly_motion.problem_exited";
            public const string ProblemNoLinkedMember = "ui.assembly_motion.problem_no_linked_member";
            public const string ProblemNoMemberNumber = "ui.assembly_motion.problem_no_member_number";
            public const string ProblemNotSignedIn = "ui.assembly_motion.problem_not_signed_in";
            public const string ProblemOutsideChapter = "ui.assembly_motion.problem_outside_chapter";
            public const string ProblemPending = "ui.assembly_motion.problem_pending";
            public const string Release = "ui.assembly_motion.release";
            public const string Reload = "ui.assembly_motion.reload";
            public const string ResultAccepted = "ui.assembly_motion.result_accepted";
            public const string ResultAcceptedModified = "ui.assembly_motion.result_accepted_modified";
            public const string ResultNotHandled = "ui.assembly_motion.result_not_handled";
            public const string ResultRejected = "ui.assembly_motion.result_rejected";
            public const string RevisionWarningMulti = "ui.assembly_motion.revision_warning_multi";
            public const string RevisionWarningMultiOne = "ui.assembly_motion.revision_warning_multi_one";
            public const string DeadlineUpToIncluding = "ui.assembly_motion.deadline_up_to_including";
            public const string RevisionWarningSingle = "ui.assembly_motion.revision_warning_single";
            public const string RevisionWarningTitle = "ui.assembly_motion.revision_warning_title";
            public const string RoleAuthor = "ui.assembly_motion.role_author";
            public const string RoleCoSubmitter = "ui.assembly_motion.role_co_submitter";
            public const string SaveChanges = "ui.assembly_motion.save_changes";
            public const string SaveDraft = "ui.assembly_motion.save_draft";
            public const string SearchPlaceholder = "ui.assembly_motion.search_placeholder";
            public const string SectionHelp = "ui.assembly_motion.section_help";
            public const string SettingsNotEnabled = "ui.assembly_motion.settings_not_enabled";
            public const string SettingsTitle = "ui.assembly_motion.settings_title";
            public const string Share = "ui.assembly_motion.share";
            public const string ShareHint = "ui.assembly_motion.share_hint";
            public const string SignInHint = "ui.assembly_motion.sign_in_hint";
            public const string SignInLink = "ui.assembly_motion.sign_in_link";
            public const string StatusAdmitted = "ui.assembly_motion.status_admitted";
            public const string StatusCollecting = "ui.assembly_motion.status_collecting";
            public const string StatusDraft = "ui.assembly_motion.status_draft";
            public const string StatusFormallyInsufficient = "ui.assembly_motion.status_formally_insufficient";
            public const string StatusInRevision = "ui.assembly_motion.status_in_revision";
            public const string StatusLapsed = "ui.assembly_motion.status_lapsed";
            public const string StatusSubmitted = "ui.assembly_motion.status_submitted";
            public const string StatusWithdrawn = "ui.assembly_motion.status_withdrawn";
            public const string Submitters = "ui.assembly_motion.submitters";
            public const string ToastDeleted = "ui.assembly_motion.toast_deleted";
            public const string ToastJoined = "ui.assembly_motion.toast_joined";
            public const string ToastLeft = "ui.assembly_motion.toast_left";
            public const string ToastLinkCopied = "ui.assembly_motion.toast_link_copied";
            public const string ToastPortalEnabled = "ui.assembly_motion.toast_portal_enabled";
            public const string ToastPortalSaved = "ui.assembly_motion.toast_portal_saved";
            public const string ToastReleased = "ui.assembly_motion.toast_released";
            public const string ToastSaved = "ui.assembly_motion.toast_saved";
            public const string ToastTypeDeleted = "ui.assembly_motion.toast_type_deleted";
            public const string ToastTypeSaved = "ui.assembly_motion.toast_type_saved";
            public const string ToastWithdrawn = "ui.assembly_motion.toast_withdrawn";
            public const string TypeCompetingDeadline = "ui.assembly_motion.type_competing_deadline";
            public const string TypeDeadline = "ui.assembly_motion.type_deadline";
            public const string TypeHasMotions = "ui.assembly_motion.type_has_motions";
            public const string TypeMinSubmitters = "ui.assembly_motion.type_min_submitters";
            public const string TypesTitle = "ui.assembly_motion.types_title";
            public const string WikiAssemblyLabel = "ui.assembly_motion.wiki_assembly_label";
            public const string WikiMirrorEnabled = "ui.assembly_motion.wiki_mirror_enabled";
            public const string WikiPagePrefix = "ui.assembly_motion.wiki_page_prefix";
            public const string WikiTemplate = "ui.assembly_motion.wiki_template";
            public const string Withdraw = "ui.assembly_motion.withdraw";
            public const string WithdrawConfirmMessage = "ui.assembly_motion.withdraw_confirm_message";
            public const string WithdrawConfirmTitle = "ui.assembly_motion.withdraw_confirm_title";
            public const string PortalButton = "ui.assembly_motion.portal_button";
            public const string ReloadConfirmMessage = "ui.assembly_motion.reload_confirm_message";
            public const string ReloadConfirmTitle = "ui.assembly_motion.reload_confirm_title";
            public const string SaveFailed = "ui.assembly_motion.save_failed";
            public const string CommissionButton = "ui.assembly_motion.commission_button";
            public const string RevisionWarningMultiAlone = "ui.assembly_motion.revision_warning_multi_alone";
            public const string EditLockedHint = "ui.assembly_motion.edit_locked_hint";
            public const string EditLockedInquiryLink = "ui.assembly_motion.edit_locked_inquiry_link";
            public const string RevisionsTitle = "ui.assembly_motion.revisions_title";
            public const string ColumnRevision = "ui.assembly_motion.column_revision";
            public const string ColumnEditedAt = "ui.assembly_motion.column_edited_at";
            public const string ColumnEditedBy = "ui.assembly_motion.column_edited_by";
            public const string ColumnChangeNote = "ui.assembly_motion.column_change_note";
            public const string EditedByCommission = "ui.assembly_motion.edited_by_commission";
            public const string EditedByAuthor = "ui.assembly_motion.edited_by_author";
            public const string ShowDiff = "ui.assembly_motion.show_diff";
            public const string DiffTitle = "ui.assembly_motion.diff_title";
            public const string DiffFirstRevision = "ui.assembly_motion.diff_first_revision";
            public const string DiffNoChanges = "ui.assembly_motion.diff_no_changes";
            public const string InquiriesTitle = "ui.assembly_motion.inquiries_title";
            public const string NoInquiries = "ui.assembly_motion.no_inquiries";
            public const string InquiryAskedAt = "ui.assembly_motion.inquiry_asked_at";
            public const string InquiryDueAt = "ui.assembly_motion.inquiry_due_at";
            public const string InquiryOpen = "ui.assembly_motion.inquiry_open";
            public const string InquiryClosed = "ui.assembly_motion.inquiry_closed";
            public const string InquiryAnswer = "ui.assembly_motion.inquiry_answer";
            public const string InquiryAnsweredBy = "ui.assembly_motion.inquiry_answered_by";
            public const string InquiryNoAnswer = "ui.assembly_motion.inquiry_no_answer";
            public const string InquiryYourAnswer = "ui.assembly_motion.inquiry_your_answer";
            public const string SendAnswer = "ui.assembly_motion.send_answer";
            public const string ToastInquiryAnswered = "ui.assembly_motion.toast_inquiry_answered";
            public const string AskInquiry = "ui.assembly_motion.ask_inquiry";
            public const string InquiryQuestion = "ui.assembly_motion.inquiry_question";
            public const string InquiryResponseDue = "ui.assembly_motion.inquiry_response_due";
            public const string InquiryDueInvalid = "ui.assembly_motion.inquiry_due_invalid";
            public const string ToastInquiryAsked = "ui.assembly_motion.toast_inquiry_asked";
            public const string CloseInquiry = "ui.assembly_motion.close_inquiry";
            public const string CloseInquiryTitle = "ui.assembly_motion.close_inquiry_title";
            public const string CloseInquiryMessage = "ui.assembly_motion.close_inquiry_message";
            public const string ToastInquiryClosed = "ui.assembly_motion.toast_inquiry_closed";
            public const string CommissionPortalsTitle = "ui.assembly_motion.commission_portals_title";
            public const string NoManagedPortals = "ui.assembly_motion.no_managed_portals";
            public const string ColumnAssembly = "ui.assembly_motion.column_assembly";
            public const string OpenDashboard = "ui.assembly_motion.open_dashboard";
            public const string CommissionDashboardTitle = "ui.assembly_motion.commission_dashboard_title";
            public const string DashboardPortalNotFound = "ui.assembly_motion.dashboard_portal_not_found";
            public const string FilterInquiryAny = "ui.assembly_motion.filter_inquiry_any";
            public const string FilterInquiryOpen = "ui.assembly_motion.filter_inquiry_open";
            public const string FilterInquiryNone = "ui.assembly_motion.filter_inquiry_none";
            public const string FilterWikiAny = "ui.assembly_motion.filter_wiki_any";
            public const string WikiStateNotMirrored = "ui.assembly_motion.wiki_state_not_mirrored";
            public const string WikiStatePending = "ui.assembly_motion.wiki_state_pending";
            public const string WikiStateSynced = "ui.assembly_motion.wiki_state_synced";
            public const string WikiStateFailed = "ui.assembly_motion.wiki_state_failed";
            public const string WikiStateProtectionFailed = "ui.assembly_motion.wiki_state_protection_failed";
            public const string ColumnWiki = "ui.assembly_motion.column_wiki";
            public const string ColumnOpenSlides = "ui.assembly_motion.column_open_slides";
            public const string DeadlinePassed = "ui.assembly_motion.deadline_passed";
            public const string FailedWikiSyncs = "ui.assembly_motion.failed_wiki_syncs";
            public const string NoDashboardMotions = "ui.assembly_motion.no_dashboard_motions";
            public const string CommissionDetailPageTitle = "ui.assembly_motion.commission_detail_page_title";
            public const string CommissionOnly = "ui.assembly_motion.commission_only";
            public const string BackToDashboard = "ui.assembly_motion.back_to_dashboard";
            public const string PublicView = "ui.assembly_motion.public_view";
            public const string MemberNumber = "ui.assembly_motion.member_number";
            public const string CheckTitle = "ui.assembly_motion.check_title";
            public const string CheckNoteLabel = "ui.assembly_motion.check_note_label";
            public const string CheckNoteRequired = "ui.assembly_motion.check_note_required";
            public const string CheckSave = "ui.assembly_motion.check_save";
            public const string CheckedBy = "ui.assembly_motion.checked_by";
            public const string ToastChecked = "ui.assembly_motion.toast_checked";
            public const string CommissionEditTitle = "ui.assembly_motion.commission_edit_title";
            public const string CommissionEditButton = "ui.assembly_motion.commission_edit_button";
            public const string ChangeNote = "ui.assembly_motion.change_note";
            public const string ChangeNoteHelp = "ui.assembly_motion.change_note_help";
            public const string ChangeNoteRequired = "ui.assembly_motion.change_note_required";
            public const string ToastCommissionEdited = "ui.assembly_motion.toast_commission_edited";
            public const string TypeLockedHint = "ui.assembly_motion.type_locked_hint";
            public const string CommissionWithdraw = "ui.assembly_motion.commission_withdraw";
            public const string CommissionWithdrawTitle = "ui.assembly_motion.commission_withdraw_title";
            public const string CommissionWithdrawMessage = "ui.assembly_motion.commission_withdraw_message";
            public const string ToastCommissionWithdrawn = "ui.assembly_motion.toast_commission_withdrawn";
            public const string WikiStateTitle = "ui.assembly_motion.wiki_state_title";
            public const string OpenSlidesTitle = "ui.assembly_motion.open_slides_title";
            public const string WikiSyncedRevision = "ui.assembly_motion.wiki_synced_revision";
            public const string WikiLastSyncedAt = "ui.assembly_motion.wiki_last_synced_at";
            public const string LastError = "ui.assembly_motion.last_error";
            public const string WikiPageTitle = "ui.assembly_motion.wiki_page_title";
            public const string WikiResync = "ui.assembly_motion.wiki_resync";
            public const string ToastWikiResync = "ui.assembly_motion.toast_wiki_resync";
            public const string WikiResyncAll = "ui.assembly_motion.wiki_resync_all";
            public const string WikiResyncAllTitle = "ui.assembly_motion.wiki_resync_all_title";
            public const string WikiResyncAllMessage = "ui.assembly_motion.wiki_resync_all_message";
            public const string ToastWikiResyncAll = "ui.assembly_motion.toast_wiki_resync_all";
            public const string ShowFailedWikiSyncs = "ui.assembly_motion.show_failed_wiki_syncs";
            public const string WikiCheckConnection = "ui.assembly_motion.wiki_check_connection";
            public const string WikiCheckConnectionHint = "ui.assembly_motion.wiki_check_connection_hint";
            public const string OpenSlidesMotionId = "ui.assembly_motion.open_slides_motion_id";
            public const string OpenSlidesPushedRevision = "ui.assembly_motion.open_slides_pushed_revision";
            public const string OpenSlidesPushedAt = "ui.assembly_motion.open_slides_pushed_at";
            public const string OpenSlidesPush = "ui.assembly_motion.open_slides_push";
            public const string OpenSlidesPushTitle = "ui.assembly_motion.open_slides_push_title";
            public const string OpenSlidesPushMessage = "ui.assembly_motion.open_slides_push_message";
            public const string OpenSlidesIncludeSubmitted = "ui.assembly_motion.open_slides_include_submitted";
            public const string OpenSlidesPushRunning = "ui.assembly_motion.open_slides_push_running";
            public const string OpenSlidesMeetingMissingHint = "ui.assembly_motion.open_slides_meeting_missing_hint";
            public const string OpenSlidesResultTitle = "ui.assembly_motion.open_slides_result_title";
            public const string OpenSlidesResultCreated = "ui.assembly_motion.open_slides_result_created";
            public const string OpenSlidesResultUpdated = "ui.assembly_motion.open_slides_result_updated";
            public const string OpenSlidesResultSkipped = "ui.assembly_motion.open_slides_result_skipped";
            public const string OpenSlidesResultFailures = "ui.assembly_motion.open_slides_result_failures";
            public const string OpenSlidesCheckConnection = "ui.assembly_motion.open_slides_check_connection";
            public const string OpenSlidesCheckConnectionHint = "ui.assembly_motion.open_slides_check_connection_hint";
            public const string OpenSlidesRevisionOutdated = "ui.assembly_motion.open_slides_revision_outdated";
            public const string ExportJson = "ui.assembly_motion.export_json";
            public const string OpenSlidesPushInterrupted = "ui.assembly_motion.open_slides_push_interrupted";
            public const string CurrentRevision = "ui.assembly_motion.current_revision";
            public const string EffectiveDeadline = "ui.assembly_motion.effective_deadline";
            public const string NoCompetitions = "ui.assembly_motion.no_competitions";
            public const string CompetitionByCommission = "ui.assembly_motion.competition_by_commission";
            public const string CompetitionRemove = "ui.assembly_motion.competition_remove";
            public const string CompetitionRemoveTitle = "ui.assembly_motion.competition_remove_title";
            public const string CompetitionRemoveMessage = "ui.assembly_motion.competition_remove_message";
            public const string CompetitionDeclareTitle = "ui.assembly_motion.competition_declare_title";
            public const string CompetitionSearchPlaceholder = "ui.assembly_motion.competition_search_placeholder";
            public const string CompetitionSearchHint = "ui.assembly_motion.competition_search_hint";
            public const string CompetitionNoMatches = "ui.assembly_motion.competition_no_matches";
            public const string CompetitionDeclare = "ui.assembly_motion.competition_declare";
            public const string ToastCompetitionDeclared = "ui.assembly_motion.toast_competition_declared";
            public const string ToastCompetitionRemoved = "ui.assembly_motion.toast_competition_removed";
            public const string CompetingBadge = "ui.assembly_motion.competing_badge";
            public const string CompetingFlag = "ui.assembly_motion.competing_flag";
            public const string CompetingFlagDeadline = "ui.assembly_motion.competing_flag_deadline";
            public const string CompetingFlagNoDeadline = "ui.assembly_motion.competing_flag_no_deadline";
            public const string ClearCompetingFlag = "ui.assembly_motion.clear_competing_flag";
            public const string ToastCompetingFlagChanged = "ui.assembly_motion.toast_competing_flag_changed";
            public const string PrioritizationTitle = "ui.assembly_motion.prioritization_title";
            public const string PrioritizationOpenHint = "ui.assembly_motion.prioritization_open_hint";
            public const string Upvote = "ui.assembly_motion.upvote";
            public const string UpvoteRemove = "ui.assembly_motion.upvote_remove";
            public const string UpvotedByYou = "ui.assembly_motion.upvoted_by_you";
            public const string UpvotedMarker = "ui.assembly_motion.upvoted_marker";
            public const string UpvoteCountText = "ui.assembly_motion.upvote_count_text";
            public const string ToastUpvoted = "ui.assembly_motion.toast_upvoted";
            public const string ToastUpvoteRemoved = "ui.assembly_motion.toast_upvote_removed";
            public const string RankingLink = "ui.assembly_motion.ranking_link";
            public const string RankingPageTitle = "ui.assembly_motion.ranking_page_title";
            public const string RankingNotAvailable = "ui.assembly_motion.ranking_not_available";
            public const string RankingNotEnabled = "ui.assembly_motion.ranking_not_enabled";
            public const string RankingTabOverall = "ui.assembly_motion.ranking_tab_overall";
            public const string RankingTabByType = "ui.assembly_motion.ranking_tab_by_type";
            public const string RankingEmpty = "ui.assembly_motion.ranking_empty";
            public const string ColumnRank = "ui.assembly_motion.column_rank";
            public const string ColumnUpvotes = "ui.assembly_motion.column_upvotes";
            public const string ColumnResult = "ui.assembly_motion.column_result";
            public const string ResultsButton = "ui.assembly_motion.results_button";
            public const string ResultsPageTitle = "ui.assembly_motion.results_page_title";
            public const string ResultNone = "ui.assembly_motion.result_none";
            public const string ResultsNoMotions = "ui.assembly_motion.results_no_motions";
            public const string ResultsSave = "ui.assembly_motion.results_save";
            public const string ResultsNoChanges = "ui.assembly_motion.results_no_changes";
            public const string ToastResultsSaved = "ui.assembly_motion.toast_results_saved";
            public const string ResultTitle = "ui.assembly_motion.result_title";
            public const string ResultSave = "ui.assembly_motion.result_save";
            public const string ToastResultSaved = "ui.assembly_motion.toast_result_saved";
            public const string CompetitionTooManyResults = "ui.assembly_motion.competition_too_many_results";
            public const string ResultsChangedCount = "ui.assembly_motion.results_changed_count";
            public const string ResultsEventNotReached = "ui.assembly_motion.results_event_not_reached";
            public const string ResultsNoEventDate = "ui.assembly_motion.results_no_event_date";
            public const string PortalOpensHint = "ui.assembly_motion.portal_opens_hint";
            public const string PortalClosedHint = "ui.assembly_motion.portal_closed_hint";
            public const string ReleaseConfirmTitle = "ui.assembly_motion.release_confirm_title";
            public const string ReleaseConfirmMessage = "ui.assembly_motion.release_confirm_message";
            public const string JoinNotEligibleTitle = "ui.assembly_motion.join_not_eligible_title";
            public const string UpvoteCountTextOne = "ui.assembly_motion.upvote_count_text_one";
            public const string RevisionWarningUnnumberedAlone = "ui.assembly_motion.revision_warning_unnumbered_alone";
            public const string RevisionWarningUnnumberedMulti = "ui.assembly_motion.revision_warning_unnumbered_multi";
        }
    }
}
