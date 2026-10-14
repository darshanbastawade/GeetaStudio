using ContentOS.Contracts;
namespace ContentOS.Application;

public interface ICreationRenderer
{
    string Key { get; }
    string Qualification { get; }
    void Validate(CreationMaterial material);
    Task<CreationRenderResult> RenderAsync(CreationRender request, CancellationToken ct);
    Task CleanupAsync(Guid creationId, CancellationToken ct);
}
public interface ICreationStudio
{
    Task<CreationWorkspace[]> WorkspacesAsync(System.Security.Claims.ClaimsPrincipal principal, CancellationToken ct);
    Task<Guid> TemplateAsync(CoreScope scope, CreationTemplateRequest request, CancellationToken ct);
    Task ControlTemplateAsync(CoreScope scope, Guid id, bool enabled, CancellationToken ct);
    Task<CreationTemplateView[]> TemplatesAsync(CustomerScope scope, CancellationToken ct);
    Task<Guid> UploadAsync(CustomerScope scope, CreationUploadRequest request, CancellationToken ct);
    Task DeleteUploadAsync(CustomerScope scope, Guid id, CancellationToken ct);
    Task<Guid> SubmitAsync(CustomerScope scope, CreationSubmit request, CancellationToken ct);
    Task<CreationView> InspectAsync(CustomerScope scope, Guid id, CancellationToken ct);
    Task<CreationView[]> ListAsync(CustomerScope scope, CancellationToken ct);
    Task EditAsync(CustomerScope scope, Guid id, CreationEdit request, CancellationToken ct);
    Task ReviewAsync(CustomerScope scope, Guid id, CreationReview request, CancellationToken ct);
    Task CancelAsync(CustomerScope scope, Guid id, CancellationToken ct);
    Task DeleteAsync(CustomerScope scope, Guid id, CancellationToken ct);
    Task<CreationPackage> ExportAsync(CustomerScope scope, Guid id, CancellationToken ct);
    Task<bool> ProcessNextAsync(CancellationToken ct);
    Task<CreationNotice[]> NoticesAsync(CustomerScope scope, CancellationToken ct);
}
