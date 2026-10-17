using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.AssemblyAssistant;

public partial class PollPanel {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public required string State { get; set; }

    [Parameter, EditorRequired]
    public required AssemblyPollSettingsDTO Settings { get; set; }

    [Parameter, EditorRequired]
    public required List<AssemblyPollDTO> Polls { get; set; }

    [Parameter]
    public bool CanControl { get; set; }

    [Parameter]
    public DateTime ServerNow { get; set; }

    [Parameter]
    public EventCallback<AssemblyPollSettingsDTO> OnSettingsChanged { get; set; }

    [Parameter]
    public EventCallback<int> OnExtend { get; set; }

    [Parameter]
    public EventCallback<int> OnStop { get; set; }

    [Parameter]
    public EventCallback<int> OnPublish { get; set; }

    [Parameter]
    public AssemblyItemKind ItemKind { get; set; }

    [Parameter]
    public bool CurrentIsMotion { get; set; }

    [Parameter]
    public bool HasCompetitors { get; set; }

    [Parameter]
    public bool ProjectorConfigured { get; set; }

    [Parameter]
    public EventCallback<(PollTemplate Template, string? Text, int? Threshold)> OnCreatePoll { get; set; }

    [Parameter]
    public EventCallback OnCreateProceduralTopics { get; set; }

    [Parameter]
    public EventCallback<int> OnReset { get; set; }

    [Parameter]
    public EventCallback<int> OnDelete { get; set; }

    private string TemplateText = "";
    private int? Threshold = 20;
    private ConfirmDialog? ResetDialog;
    private ConfirmDialog? DeleteDialog;

    private IEnumerable<PollTemplate> Templates() {
        if (ItemKind == AssemblyItemKind.Election)
            return [PollTemplate.Support, PollTemplate.QuestionRound, PollTemplate.GoMotion, PollTemplate.Opinion];
        if (!CurrentIsMotion)
            return [PollTemplate.GoMotion, PollTemplate.Opinion, PollTemplate.Support, PollTemplate.Block];

        var motion = new List<PollTemplate> { PollTemplate.ContinueDebate, PollTemplate.GoMotion, PollTemplate.Opinion, PollTemplate.Controversy };
        if (HasCompetitors) {
            motion.Add(PollTemplate.Competition);
            motion.Add(PollTemplate.Runoff);
        }
        motion.Add(PollTemplate.FinalVote);
        return motion;
    }

    private static string TemplateKey(PollTemplate template) {
        return template switch {
            PollTemplate.ContinueDebate => I18nKey.Ui.AssemblyConsole.TemplateContinueDebate,
            PollTemplate.GoMotion => I18nKey.Ui.AssemblyConsole.TemplateGoMotion,
            PollTemplate.Opinion => I18nKey.Ui.AssemblyConsole.TemplateOpinion,
            PollTemplate.Support => I18nKey.Ui.AssemblyConsole.TemplateSupport,
            PollTemplate.Controversy => I18nKey.Ui.AssemblyConsole.TemplateControversy,
            PollTemplate.Competition => I18nKey.Ui.AssemblyConsole.TemplateCompetition,
            PollTemplate.Runoff => I18nKey.Ui.AssemblyConsole.TemplateRunoff,
            PollTemplate.FinalVote => I18nKey.Ui.AssemblyConsole.TemplateFinalVote,
            PollTemplate.QuestionRound => I18nKey.Ui.AssemblyConsole.TemplateQuestionRound,
            _ => I18nKey.Ui.AssemblyConsole.TemplateBlock
        };
    }

    private async Task Create(PollTemplate template) {
        var text = string.IsNullOrWhiteSpace(TemplateText) ? null : TemplateText.Trim();
        await OnCreatePoll.InvokeAsync((template, text, template == PollTemplate.Support ? Threshold : null));
        TemplateText = "";
    }

    private async Task Reset(int pollId) {
        if (ResetDialog != null && await ResetDialog.ShowAsync())
            await OnReset.InvokeAsync(pollId);
    }

    private async Task Delete(int pollId) {
        if (DeleteDialog != null && await DeleteDialog.ShowAsync())
            await OnDelete.InvokeAsync(pollId);
    }

    private string StateKey() {
        return State switch {
            "Connected" => I18nKey.Ui.AssemblyConsole.OpenSlidesConnected,
            "Connecting" => I18nKey.Ui.AssemblyConsole.OpenSlidesConnecting,
            _ => I18nKey.Ui.AssemblyConsole.OpenSlidesDisconnected
        };
    }

    private static string ResultClass(bool? passed) {
        if (passed == null)
            return "text-body";

        return passed.Value ? "text-success" : "text-danger";
    }

    private Task ChangeDuration(string? value) {
        if (!int.TryParse(value, out var seconds))
            return Task.CompletedTask;

        return Change(durationSeconds: seconds);
    }

    private Task Change(int? durationSeconds = null, bool? autoStart = null, bool? autoStop = null, bool? autoPublish = null, bool? project = null) {
        return OnSettingsChanged.InvokeAsync(new AssemblyPollSettingsDTO {
            DurationSeconds = durationSeconds ?? Settings.DurationSeconds,
            AutoStart = autoStart ?? Settings.AutoStart,
            AutoStop = autoStop ?? Settings.AutoStop,
            AutoPublish = autoPublish ?? Settings.AutoPublish,
            Project = project ?? Settings.Project
        });
    }
}
