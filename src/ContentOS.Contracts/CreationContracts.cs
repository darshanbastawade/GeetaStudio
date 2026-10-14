namespace ContentOS.Contracts;

public sealed record CreationTemplateRequest(Guid OperationId, Guid UniverseId, string Key, int Version, string Format, Guid EditorialItemId, Guid RevisionId, string[] AgeBands, string[] Themes, string Adapter, long Credits, string Evidence, Guid[] MediaVersionIds, Guid? WatchProductionId = null, Guid? CharacterProfileRevisionId = null);
public sealed record CreationTemplateView(Guid Id, CreationTemplateRequest Settings, bool Enabled);
public sealed record CreationInput(string Nickname, string Theme, string Reflection = "");
public sealed record CreationSubmit(Guid OperationId, Guid TemplateId, Guid ProfileId, CreationInput Input, Guid[] UploadIds);
public sealed record CreationEdit(Guid OperationId, string RowVersion, CreationInput Input);
public sealed record CreationUploadRequest(Guid OperationId, Guid ProfileId, string MediaType, byte[] Content, string AltText, string Rights, bool Consent, bool RealPerson = false);
public sealed record CreationReview(Guid OperationId, string RowVersion, string Decision, string Evidence, string Mode = "Fixture", bool SharingConsent = false);
public sealed record CreationView(Guid Id, Guid TemplateId, string State, string Outcome, int Version, string RowVersion, long EstimatedCredits, long ChargedCredits, int QueuePosition, string Review, bool PublicEligible, string? Html, CitationExport[] Citations, Guid? ReservationId, string Adapter, string Cleanup, CreationInput? Input = null);
public sealed record CreationPackage(Guid CreationId, int Version, string Sha256, string Html, CitationExport[] Citations, string LicenseNote, bool PublicEligible, Guid SourceRevisionId, CreationVideo[]? Videos = null);
public sealed record CreationVideo(Guid ArtifactId, string Sha256, byte[] Content, string Vtt, string Srt);
public sealed record CreationRender(Guid Id, string Format, CreationInput Input, string SourceTitle, string SourceBody, CitationExport[] Citations, CreationMaterial[] Materials, string CharacterName = "Reviewed fictional character");
public sealed record CreationMaterial(string MediaType, byte[] Content, string AltText, string Rights, string Credits = "Owned upload; owner/guardian attested");
public sealed record CreationRenderResult(string Html, decimal Cost, string Currency);
public sealed record CreationNotice(Guid Id, Guid CreationId, string Kind, DateTimeOffset CreatedUtc);
public sealed record CreationWorkspace(Guid Id, string Name);
