using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Settings;

namespace Quartermaster.Blazor.Pages.Administration.Settings;

/// <summary>
/// Every settings assistant with the options it edits. A test compares the option definitions against this catalog, so a
/// new option fails the build until an assistant covers it or it is listed in <see cref="AdvancedOnlyKeys"/>.
/// </summary>
public static class SettingsAssistantCatalog {
    private static readonly IReadOnlyList<SettingsChoice> XReplyChoices = [
        new("everyone", I18nKey.Ui.Settings.XReplyEveryone),
        new("following", I18nKey.Ui.Settings.XReplyFollowing),
        new("mentionedUsers", I18nKey.Ui.Settings.XReplyMentioned),
        new("subscribers", I18nKey.Ui.Settings.XReplySubscribers),
        new("verified", I18nKey.Ui.Settings.XReplyVerified)
    ];

    public static readonly SettingsAssistant General = new("General", SettingsGroup.Basics, "bi-sliders", I18nKey.Ui.Settings.GeneralTitle, I18nKey.Ui.Settings.GeneralDescription) {
        Sections = [
            new([
                new("system.app_name"),
                new("system.public_base_url", SettingsFieldKind.BaseUrl),
                new("general.chaptername.display"),
                new("general.contact.email"),
                new("general.error.contact") { FullWidth = true }
            ])
        ],
        RequiredKeys = ["system.public_base_url", "general.contact.email"]
    };

    public static readonly SettingsAssistant PublicForms = new("PublicForms", SettingsGroup.Basics, "bi-ui-checks", I18nKey.Ui.Settings.PublicFormsTitle, I18nKey.Ui.Settings.PublicFormsDescription) {
        ManagedKeys = ["public_forms.membership_application.enabled", "public_forms.due_selection.enabled", "public_forms.motion.enabled"],
        ToggleKeys = ["public_forms.membership_application.enabled", "public_forms.due_selection.enabled", "public_forms.motion.enabled"]
    };

    public static readonly SettingsAssistant TravelExpenses = new("TravelExpenses", SettingsGroup.Basics, "bi-suitcase-lg", I18nKey.Ui.Settings.TravelExpensesTitle, I18nKey.Ui.Settings.TravelExpensesDescription) {
        Sections = [
            new([
                new("forms.receipts.max_per_user", SettingsFieldKind.Number),
                new("forms.receipts.max_file_mb", SettingsFieldKind.Number) { UnitKey = I18nKey.Ui.Settings.UnitMegabytes }
            ]) { TitleKey = I18nKey.Ui.Settings.TravelExpensesReceiptsTitle, DescriptionKey = I18nKey.Ui.Settings.TravelExpensesReceiptsDescription }
        ],
        Links = [new("/Administration/TravelExpenseRules", I18nKey.Ui.Settings.LinkTravelExpenseRules)]
    };

    public static readonly SettingsAssistant Saml = new("Saml", SettingsGroup.Login, "bi-shield-lock", I18nKey.Ui.Settings.SamlTitle, I18nKey.Ui.Settings.SamlDescription) {
        Sections = [
            new([
                new("auth.saml.endpoint"),
                new("auth.saml.client_id"),
                new("auth.saml.certificate"),
                new("auth.saml.button_text"),
                new("auth.saml.expected_audience"),
                new("auth.saml.expected_destination"),
                new("auth.saml.member_number_attribute"),
                new("auth.saml.department_attribute"),
                new("auth.saml.eligible_attribute"),
                new("auth.sso.support_contact")
            ])
        ],
        RequiredKeys = ["auth.saml.endpoint", "auth.saml.client_id", "auth.saml.certificate"]
    };

    public static readonly SettingsAssistant Oidc = new("Oidc", SettingsGroup.Login, "bi-key", I18nKey.Ui.Settings.OidcTitle, I18nKey.Ui.Settings.OidcDescription) {
        Sections = [
            new([
                new("auth.oidc.authority"),
                new("auth.oidc.client_id"),
                new("auth.oidc.client_secret"),
                new("auth.oidc.button_text"),
                new("auth.oidc.member_number_claim"),
                new("auth.oidc.department_claim"),
                new("auth.oidc.eligible_claim"),
                new("auth.sso.support_contact")
            ])
        ],
        RequiredKeys = ["auth.oidc.authority", "auth.oidc.client_id", "auth.oidc.client_secret"]
    };

    public static readonly SettingsAssistant Security = new("Security", SettingsGroup.Login, "bi-shield-check", I18nKey.Ui.Settings.SecurityTitle, I18nKey.Ui.Settings.SecurityDescription) {
        Sections = [
            new([
                new("auth.token.lifetime_days", SettingsFieldKind.Number) { UnitKey = I18nKey.Ui.Settings.UnitDays }
            ]) { TitleKey = I18nKey.Ui.Settings.SecuritySessionsTitle },
            new([
                new("auth.lockout.max_attempts", SettingsFieldKind.Number),
                new("auth.lockout.duration_minutes", SettingsFieldKind.Number) { UnitKey = I18nKey.Ui.Settings.UnitMinutes }
            ]) { TitleKey = I18nKey.Ui.Settings.SecurityLockoutTitle },
            new([
                new("auth.ratelimit.anonymous_create_permits", SettingsFieldKind.Number),
                new("auth.ratelimit.anonymous_create_window_minutes", SettingsFieldKind.Number) { UnitKey = I18nKey.Ui.Settings.UnitMinutes },
                new("auth.ratelimit.due_selection_permits", SettingsFieldKind.Number),
                new("auth.ratelimit.due_selection_window_minutes", SettingsFieldKind.Number) { UnitKey = I18nKey.Ui.Settings.UnitMinutes }
            ]) { TitleKey = I18nKey.Ui.Settings.SecurityRateLimitTitle }
        ]
    };

    public static readonly SettingsAssistant Smtp = new("Smtp", SettingsGroup.Communication, "bi-envelope-gear", I18nKey.Ui.Settings.SmtpTitle, I18nKey.Ui.Settings.SmtpDescription) {
        Sections = [
            new([
                new("email.smtp.host"),
                new("email.smtp.port", SettingsFieldKind.Number),
                new("email.smtp.use_ssl", SettingsFieldKind.Bool),
                new("email.smtp.username"),
                new("email.smtp.password"),
                new("email.smtp.sender_address"),
                new("email.smtp.sender_name")
            ])
        ],
        RequiredKeys = ["email.smtp.host", "email.smtp.sender_address"]
    };

    public static readonly SettingsAssistant Imap = new("Imap", SettingsGroup.Communication, "bi-inboxes", I18nKey.Ui.Settings.ImapTitle, I18nKey.Ui.Settings.ImapDescription) {
        Sections = [
            new([
                new("email.imap.host"),
                new("email.imap.port", SettingsFieldKind.Number),
                new("email.imap.use_ssl", SettingsFieldKind.Bool),
                new("email.imap.username"),
                new("email.imap.password"),
                new("email.imap.mailbox"),
                new("email.imap.poll_interval_minutes", SettingsFieldKind.Number) { UnitKey = I18nKey.Ui.Settings.UnitMinutes }
            ]),
            new([
                new("email.mdn.request_default", SettingsFieldKind.Bool),
                new("email.mdn.receipt_address")
            ])
        ],
        RequiredKeys = ["email.imap.host", "email.imap.username", "email.imap.password"]
    };

    public static readonly SettingsAssistant Telegram = new("Telegram", SettingsGroup.Communication, "bi-telegram", I18nKey.Ui.Settings.TelegramTitle, I18nKey.Ui.Settings.TelegramDescription) {
        Sections = [
            new([
                new("messaging.telegram.bot_token"),
                new("messaging.telegram.bot_username") { FillFromCheck = true }
            ]) { Check = SettingsCheckKind.Telegram }
        ],
        RequiredKeys = ["messaging.telegram.bot_token", "messaging.telegram.bot_username"]
    };

    public static readonly SettingsAssistant MemberImport = new("MemberImport", SettingsGroup.Communication, "bi-people", I18nKey.Ui.Settings.MemberImportTitle, I18nKey.Ui.Settings.MemberImportDescription) {
        Sections = [
            new([
                new("member_import.file_path") { FullWidth = true },
                new("member_import.polling_interval_minutes", SettingsFieldKind.Number) { UnitKey = I18nKey.Ui.Settings.UnitMinutes }
            ]) { Check = SettingsCheckKind.MemberImport }
        ],
        RequiredKeys = ["member_import.file_path"],
        Links = [new("/Administration/Members/Import", I18nKey.Ui.Settings.LinkImportHistory)]
    };

    public static readonly SettingsAssistant Keycloak = new("Keycloak", SettingsGroup.Integrations, "bi-person-badge", I18nKey.Ui.Settings.KeycloakTitle, I18nKey.Ui.Settings.KeycloakDescription) {
        Sections = [
            new([
                new("keycloak.base_url"),
                new("keycloak.auth_realm"),
                new("keycloak.realm"),
                new("keycloak.client_id"),
                new("keycloak.client_secret"),
                new("keycloak.username_separator")
            ]) { Check = SettingsCheckKind.Keycloak }
        ],
        RequiredKeys = ["keycloak.base_url", "keycloak.realm", "keycloak.client_id", "keycloak.client_secret"]
    };

    public static readonly SettingsAssistant Wiki = new("Wiki", SettingsGroup.Integrations, "bi-journal-text", I18nKey.Ui.Settings.WikiTitle, I18nKey.Ui.Settings.WikiDescription) {
        Sections = [
            new([
                new("wiki.api_url"),
                new("wiki.protection_level"),
                new("wiki.bot_username"),
                new("wiki.bot_password")
            ]) { Check = SettingsCheckKind.Wiki }
        ],
        RequiredKeys = ["wiki.api_url", "wiki.bot_username", "wiki.bot_password"]
    };

    public static readonly SettingsAssistant Forum = new("Forum", SettingsGroup.Integrations, "bi-chat-square-text", I18nKey.Ui.Settings.ForumTitle, I18nKey.Ui.Settings.ForumDescription) {
        Sections = [
            new([
                new("discourse.base_url") { FullWidth = true },
                new("discourse.api_username"),
                new("discourse.api_key"),
                new("discourse.max_post_length")
            ]) { Check = SettingsCheckKind.Forum }
        ],
        RequiredKeys = ["discourse.base_url", "discourse.api_username", "discourse.api_key"]
    };

    public static readonly SettingsAssistant Redmine = new("Redmine", SettingsGroup.Integrations, "bi-ticket-detailed", I18nKey.Ui.Settings.RedmineTitle, I18nKey.Ui.Settings.RedmineDescription);

    public static readonly SettingsAssistant Forwarding = new("Forwarding", SettingsGroup.Integrations, "bi-signpost-split", I18nKey.Ui.Settings.ForwardingTitle, I18nKey.Ui.Settings.ForwardingDescription) {
        ManagedKeys = [
            ForwardingOptionKeys.MembershipApplicationEnabled,
            ForwardingOptionKeys.DueSelectionEnabled,
            ForwardingOptionKeys.RegularMinimum,
            ForwardingOptionKeys.StateDecisionMinimum
        ],
        ToggleKeys = [ForwardingOptionKeys.MembershipApplicationEnabled, ForwardingOptionKeys.DueSelectionEnabled],
        Links = [new("/Administration/Forwarding", I18nKey.Ui.Settings.LinkForwardingCases)]
    };

    public static readonly SettingsAssistant OpenSlides = new("OpenSlides", SettingsGroup.Integrations, "bi-easel", I18nKey.Ui.Settings.OpenSlidesTitle, I18nKey.Ui.Settings.OpenSlidesDescription) {
        Sections = [
            new([
                new("openslides.url") { FullWidth = true },
                new("openslides.username"),
                new("openslides.password")
            ]) { Check = SettingsCheckKind.OpenSlides }
        ],
        RequiredKeys = ["openslides.url", "openslides.username", "openslides.password"]
    };

    public static readonly SettingsAssistant SocialMedia = new("SocialMedia", SettingsGroup.Integrations, "bi-megaphone", I18nKey.Ui.Settings.SocialMediaTitle, I18nKey.Ui.Settings.SocialMediaDescription) {
        Sections = [
            new([
                new("newsposts.mistral.api_key") { FullWidth = true },
                new("newsposts.mistral.triage_model", SettingsFieldKind.Suggest),
                new("newsposts.mistral.generation_model", SettingsFieldKind.Suggest),
                new("newsposts.mistral.embedding_model", SettingsFieldKind.Suggest)
            ]) { TitleKey = I18nKey.Ui.Settings.SocialMediaMistralTitle, DescriptionKey = I18nKey.Ui.Settings.SocialMediaMistralDescription, Check = SettingsCheckKind.Mistral },
            new([
                new("newsposts.postiz.base_url") { FullWidth = true },
                new("newsposts.postiz.api_key"),
                new("newsposts.postiz.x_reply_setting", SettingsFieldKind.Select) { Choices = XReplyChoices }
            ]) { TitleKey = I18nKey.Ui.Settings.SocialMediaPostizTitle, Check = SettingsCheckKind.Postiz },
            new([
                new("newsposts.enabled", SettingsFieldKind.Bool),
                new("newsposts.poll_interval_minutes", SettingsFieldKind.Number) { UnitKey = I18nKey.Ui.Settings.UnitMinutes },
                new("newsposts.max_item_age_hours", SettingsFieldKind.Number) { UnitKey = I18nKey.Ui.Settings.UnitHours },
                new("newsposts.retention_days", SettingsFieldKind.Number) { UnitKey = I18nKey.Ui.Settings.UnitDays },
                new("newsposts.triage.min_similarity", SettingsFieldKind.Number) { Step = "0.01" },
                new("newsposts.generation.candidate_count", SettingsFieldKind.Number),
                new("newsposts.generation.prompt", SettingsFieldKind.Multiline)
            ]) { TitleKey = I18nKey.Ui.Settings.SocialMediaPipelineTitle }
        ],
        ToggleKeys = ["newsposts.enabled"],
        Links = [new("/Administration/NewsPosts/Settings", I18nKey.Ui.Settings.LinkNewsFeeds)]
    };

    public static readonly SettingsAssistant AssemblyAssistant = new("AssemblyAssistant", SettingsGroup.Assembly, "bi-mic", I18nKey.Ui.Settings.AssemblyAssistantTitle, I18nKey.Ui.Settings.AssemblyAssistantDescription) {
        Sections = [
            new([
                new("mumble.host"),
                new("mumble.port", SettingsFieldKind.Number),
                new("mumble.username"),
                new("mumble.server_certificate_sha256")
            ])
        ],
        ManagedKeys = [
            "mumble.certificate", "mumble.certificate_password", "mumble.channels.chair", "mumble.channels.speaker",
            "mumble.channels.motion_author", "mumble.channels.pro", "mumble.channels.contra", "mumble.channels.question",
            "mumble.channels.point", "mumble.channels.listener"
        ],
        RequiredKeys = ["mumble.host", "mumble.certificate", "mumble.channels.chair", "mumble.channels.speaker", "mumble.channels.listener"]
    };

    public static readonly SettingsAssistant AssemblyProcedure = new("AssemblyProcedure", SettingsGroup.Assembly, "bi-stopwatch", I18nKey.Ui.Settings.AssemblyProcedureTitle, I18nKey.Ui.Settings.AssemblyProcedureDescription) {
        Sections = [
            new([
                new("assembly.times.presentation", SettingsFieldKind.Duration),
                new("assembly.times.speech", SettingsFieldKind.Duration),
                new("assembly.times.question", SettingsFieldKind.Duration),
                new("assembly.times.statement", SettingsFieldKind.Duration),
                new("assembly.times.closing_word", SettingsFieldKind.Duration),
                new("assembly.times.candidate_presentation", SettingsFieldKind.Duration),
                new("assembly.times.candidate_presentation_repeat", SettingsFieldKind.Duration),
                new("assembly.times.candidate_question", SettingsFieldKind.Duration),
                new("assembly.times.candidate_answer", SettingsFieldKind.Duration)
            ]) { TitleKey = I18nKey.Ui.Settings.AssemblyTimesTitle },
            new([
                new("assembly.grace_seconds", SettingsFieldKind.Number) { UnitKey = I18nKey.Ui.Settings.UnitSeconds },
                new("assembly.start_now_after_seconds", SettingsFieldKind.Number) { UnitKey = I18nKey.Ui.Settings.UnitSeconds },
                new("assembly.messages.warning"),
                new("assembly.messages.expired")
            ]) { TitleKey = I18nKey.Ui.Settings.AssemblySpeakersTitle },
            new([
                new("assembly.poll.duration_seconds", SettingsFieldKind.Number) { UnitKey = I18nKey.Ui.Settings.UnitSeconds },
                new("assembly.openslides.delegates_group_id", SettingsFieldKind.Number),
                new("assembly.poll.auto_start", SettingsFieldKind.Bool),
                new("assembly.poll.auto_stop", SettingsFieldKind.Bool),
                new("assembly.poll.auto_publish", SettingsFieldKind.Bool),
                new("assembly.poll.project", SettingsFieldKind.Bool),
                new("assembly.openslides.projector_ids")
            ]) { TitleKey = I18nKey.Ui.Settings.AssemblyPollsTitle }
        ]
    };

    public static readonly IReadOnlyList<SettingsAssistant> All = [
        General, PublicForms, TravelExpenses, Saml, Oidc, Security, Smtp, Imap, Telegram, MemberImport, Keycloak, Wiki, Forum, Redmine, Forwarding, OpenSlides, SocialMedia,
        AssemblyAssistant, AssemblyProcedure
    ];

    /// <summary>Options only reachable through the advanced settings: debug switches, tuning values and server paths.</summary>
    public static readonly IReadOnlyList<string> AdvancedOnlyKeys = [
        "general.error.show_details",
        "email.smtp.batch_size",
        "newsposts.mistral.base_url",
        "assembly.voice_hangover_ms",
        "meetings.collab.save_interval_seconds",
        "meetings.motion_notes_template",
        "messaging.pdf.output_dir",
        "meetings.protocol.archive_dir",
        "support_signatures.form_max_megabytes"
    ];

    public static SettingsAssistant? BySlug(string slug) {
        return All.FirstOrDefault(a => string.Equals(a.Slug, slug, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The first assistant covering the option, for the link on the advanced page.</summary>
    public static SettingsAssistant? ForKey(string key) {
        return All.FirstOrDefault(a => a.AllKeys().Contains(key));
    }
}
