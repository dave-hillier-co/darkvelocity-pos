using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Services;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class PurchaseDocumentEndpoints
{
    public static WebApplication MapPurchaseDocumentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/purchases").WithTags("PurchaseDocuments");

        // Upload a new purchase document
        group.MapPost("/", async (
            Guid orgId,
            Guid siteId,
            IFormFile file,
            [FromQuery] PurchaseDocumentType? type,
            [FromQuery] DocumentSource? source,
            IGrainFactory grainFactory,
            IDocumentIntelligenceService documentService) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(Hal.Error("invalid_file", "No file uploaded"));

            var documentId = Guid.NewGuid();
            var documentType = type ?? PurchaseDocumentType.Invoice;
            var documentSource = source ?? DocumentSource.Upload;

            // TODO: In production, store file in blob storage and get URL
            var storageUrl = $"/storage/purchases/{orgId}/{siteId}/{documentId}/{file.FileName}";

            var grain = grainFactory.GetGrain<IPurchaseDocumentGrain>(
                GrainKeys.PurchaseDocument(orgId, siteId, documentId));

            var snapshot = await grain.ReceiveAsync(new ReceivePurchaseDocumentCommand(
                orgId,
                siteId,
                documentId,
                documentType,
                documentSource,
                storageUrl,
                file.FileName,
                file.ContentType,
                file.Length));

            // Auto-process the document
            await grain.RequestProcessingAsync();

            // Extract using document intelligence service
            using var stream = file.OpenReadStream();
            var extractionResult = await documentService.ExtractAsync(stream, file.ContentType, documentType);

            await grain.ApplyExtractionResultAsync(new ApplyExtractionResultCommand(
                extractionResult.ToExtractedDocumentData(),
                extractionResult.OverallConfidence,
                "stub-v1"));

            snapshot = await grain.GetSnapshotAsync();

            return Results.Created(
                $"/api/orgs/{orgId}/sites/{siteId}/purchases/{documentId}",
                Hal.Resource(snapshot, BuildDocumentLinks(orgId, siteId, documentId)));
        })
        .DisableAntiforgery(); // Required for file uploads

        // Get document by ID
        group.MapGet("/{documentId}", async (
            Guid orgId,
            Guid siteId,
            Guid documentId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPurchaseDocumentGrain>(
                GrainKeys.PurchaseDocument(orgId, siteId, documentId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Purchase document not found"));

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildDocumentLinks(orgId, siteId, documentId)));
        });

        // Trigger (re)processing
        group.MapPost("/{documentId}/process", async (
            Guid orgId,
            Guid siteId,
            Guid documentId,
            IGrainFactory grainFactory,
            IDocumentIntelligenceService documentService) =>
        {
            var grain = grainFactory.GetGrain<IPurchaseDocumentGrain>(
                GrainKeys.PurchaseDocument(orgId, siteId, documentId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Purchase document not found"));

            var state = await grain.GetStateAsync();
            await grain.RequestProcessingAsync();

            // TODO: In production, fetch file from blob storage
            // For now, use stub which doesn't need the actual file
            using var stream = new MemoryStream();
            var extractionResult = await documentService.ExtractAsync(
                stream, state.ContentType, state.DocumentType);

            await grain.ApplyExtractionResultAsync(new ApplyExtractionResultCommand(
                extractionResult.ToExtractedDocumentData(),
                extractionResult.OverallConfidence,
                "stub-v1"));

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildDocumentLinks(orgId, siteId, documentId)));
        });

        // Map a line item to an SKU
        group.MapPatch("/{documentId}/lines/{lineIndex}", async (
            Guid orgId,
            Guid siteId,
            Guid documentId,
            int lineIndex,
            [FromBody] MapLineRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPurchaseDocumentGrain>(
                GrainKeys.PurchaseDocument(orgId, siteId, documentId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Purchase document not found"));

            await grain.MapLineAsync(new MapLineCommand(
                lineIndex,
                request.IngredientId,
                request.IngredientSku,
                request.IngredientName,
                request.Source ?? MappingSource.Manual,
                request.Confidence ?? 1.0m));

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildDocumentLinks(orgId, siteId, documentId)));
        });

        // Update a line item
        group.MapPut("/{documentId}/lines/{lineIndex}", async (
            Guid orgId,
            Guid siteId,
            Guid documentId,
            int lineIndex,
            [FromBody] UpdateLineRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPurchaseDocumentGrain>(
                GrainKeys.PurchaseDocument(orgId, siteId, documentId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Purchase document not found"));

            await grain.UpdateLineAsync(new UpdatePurchaseLineCommand(
                lineIndex,
                request.Description,
                request.Quantity,
                request.Unit,
                request.UnitPrice));

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildDocumentLinks(orgId, siteId, documentId)));
        });

        // Confirm document
        group.MapPost("/{documentId}/confirm", async (
            Guid orgId,
            Guid siteId,
            Guid documentId,
            [FromBody] ConfirmDocumentRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPurchaseDocumentGrain>(
                GrainKeys.PurchaseDocument(orgId, siteId, documentId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Purchase document not found"));

            var snapshot = await grain.ConfirmAsync(new ConfirmPurchaseDocumentCommand(
                request.ConfirmedBy,
                request.VendorId,
                request.VendorName,
                request.DocumentDate,
                request.Currency));

            return Results.Ok(Hal.Resource(snapshot, BuildDocumentLinks(orgId, siteId, documentId)));
        });

        // Reject/delete document
        group.MapDelete("/{documentId}", async (
            Guid orgId,
            Guid siteId,
            Guid documentId,
            [FromBody] RejectDocumentRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPurchaseDocumentGrain>(
                GrainKeys.PurchaseDocument(orgId, siteId, documentId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Purchase document not found"));

            await grain.RejectAsync(new RejectPurchaseDocumentCommand(request.RejectedBy, request.Reason));
            return Results.NoContent();
        });

        return app;
    }

    private static Dictionary<string, object> BuildDocumentLinks(Guid orgId, Guid siteId, Guid documentId)
    {
        var basePath = $"/api/orgs/{orgId}/sites/{siteId}/purchases/{documentId}";
        return new Dictionary<string, object>
        {
            ["self"] = new { href = basePath },
            ["process"] = new { href = $"{basePath}/process" },
            ["confirm"] = new { href = $"{basePath}/confirm" }
        };
    }
}
