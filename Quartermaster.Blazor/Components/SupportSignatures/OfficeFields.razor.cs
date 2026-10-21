using System;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.SupportSignatures;

public partial class OfficeFields {
    private readonly string _id = Guid.NewGuid().ToString("N")[..8];

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public CertificationOfficeSaveRequest Request { get; set; } = new();
}
