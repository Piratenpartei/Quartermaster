using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.Forms;

/// <summary>Builds the <see cref="WizardStep"/>s of the public forms: a step is complete by a rule on the entry state, and Next on an incomplete step shows its message.</summary>
public class PublicFormStepBuilder {
    /// <summary>The parameter name every step component takes its entry state under.</summary>
    public const string StateParameter = "State";

    private readonly I18nService _i18n;
    private readonly ToastService _toasts;

    public PublicFormStepBuilder(I18nService i18n, ToastService toasts) {
        _i18n = i18n;
        _toasts = toasts;
    }

    public WizardStep Step(string titleKey, RenderFragment content, Func<bool> isComplete, string? incompleteKey) {
        return new WizardStep {
            Title = _i18n[titleKey],
            Content = content,
            IsValid = isComplete,
            Validate = () => Task.FromResult(Check(isComplete, incompleteKey))
        };
    }

    public WizardStep StateStep<TStep>(string titleKey, object state, Func<bool> isComplete, string? incompleteKey) where TStep : IComponent {
        return Step(titleKey, builder => {
            builder.OpenComponent<TStep>(0);
            builder.AddComponentParameter(1, StateParameter, state);
            builder.CloseComponent();
        }, isComplete, incompleteKey);
    }

    private bool Check(Func<bool> isComplete, string? incompleteKey) {
        if (isComplete())
            return true;
        if (incompleteKey != null)
            _toasts.ErrorKey(incompleteKey);

        return false;
    }
}
