using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public abstract class AssemblyMotionMemberEndpoint<TRequest> : Endpoint<TRequest, AssemblyMotionDetailDTO> where TRequest : notnull {
    protected async Task SendResultAsync(AssemblyMotionActionResult result, CancellationToken ct) {
        if (result.Detail != null) {
            await SendAsync(result.Detail, cancellation: ct);
        } else if (result.ErrorKey != null) {
            AddError(result.ErrorKey);
            await SendErrorsAsync(result.StatusCode, ct);
        } else if (result.StatusCode == 404) {
            await SendNotFoundAsync(ct);
        } else {
            await SendNoContentAsync(ct);
        }
    }
}
