using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Options;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Options;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Options;

public class OptionListEndpoint : EndpointWithoutRequest<List<OptionDefinitionDTO>> {
    private readonly OptionRepository _optionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public OptionListEndpoint(OptionRepository optionRepo, ChapterRepository chapterRepo,
        UserGlobalPermissionRepository globalPermRepo) {
        _optionRepo = optionRepo;
        _chapterRepo = chapterRepo;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Get("/api/options");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.ViewOptions, _globalPermRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var definitions = _optionRepo.GetAllDefinitions();
        var allValues = _optionRepo.GetAllValues();
        var chapters = _chapterRepo.GetAll().ToDictionary(c => c.Id);

        var dtos = definitions.Select(def => {
            var values = allValues.Where(v => v.Identifier == def.Identifier).ToList();
            var globalValue = values.FirstOrDefault(v => v.ChapterId == null)?.Value ?? "";
            var overrides = values
                .Where(v => v.ChapterId != null && chapters.ContainsKey(v.ChapterId.Value))
                .Select(v => {
                    var ch = chapters[v.ChapterId!.Value];
                    return new OptionOverrideDTO {
                        ChapterId = ch.Id,
                        ChapterName = ch.Name,
                        ChapterShortCode = ch.ShortCode ?? "",
                        Value = v.Value
                    };
                }).ToList();

            return new OptionDefinitionDTO {
                Identifier = def.Identifier,
                FriendlyName = def.FriendlyName,
                DataType = (int)def.DataType,
                IsOverridable = def.IsOverridable,
                TemplateModels = def.TemplateModels,
                GlobalValue = globalValue,
                Overrides = overrides
            };
        }).ToList();

        await SendAsync(dtos, cancellation: ct);
    }
}
