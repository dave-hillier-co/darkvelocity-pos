using DarkVelocity.Host;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class PurchaseDocumentGrainTests
{
    private readonly TestClusterFixture _fixture;

    public PurchaseDocumentGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IPurchaseDocumentGrain GetGrain(Guid orgId, Guid siteId, Guid documentId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IPurchaseDocumentGrain>(
            GrainKeys.PurchaseDocument(orgId, siteId, documentId));
    }

    [Fact]
    public async Task ReceiveAsync_Invoice_ShouldInitializeDocument()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, documentId);

        // Act
        var snapshot = await grain.ReceiveAsync(new ReceivePurchaseDocumentCommand(
            orgId,
            siteId,
            documentId,
            PurchaseDocumentType.Invoice,
            DocumentSource.Email,
            "/storage/invoices/test.pdf",
            "test-invoice.pdf",
            "application/pdf",
            1024));

        // Assert
        snapshot.DocumentId.Should().Be(documentId);
        snapshot.DocumentType.Should().Be(PurchaseDocumentType.Invoice);
        snapshot.Source.Should().Be(DocumentSource.Email);
        snapshot.Status.Should().Be(PurchaseDocumentStatus.Received);
        snapshot.IsPaid.Should().BeFalse(); // Invoices default to unpaid
    }

    [Fact]
    public async Task ReceiveAsync_Receipt_ShouldInitializeAsPaid()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, documentId);

        // Act
        var snapshot = await grain.ReceiveAsync(new ReceivePurchaseDocumentCommand(
            orgId,
            siteId,
            documentId,
            PurchaseDocumentType.Receipt,
            DocumentSource.Photo,
            "/storage/receipts/test.jpg",
            "costco-receipt.jpg",
            "image/jpeg",
            512000));

        // Assert
        snapshot.DocumentType.Should().Be(PurchaseDocumentType.Receipt);
        snapshot.Source.Should().Be(DocumentSource.Photo);
        snapshot.IsPaid.Should().BeTrue(); // Receipts default to paid
    }

    [Fact]
    public async Task ReceiveAsync_AlreadyExists_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, documentId);

        await grain.ReceiveAsync(new ReceivePurchaseDocumentCommand(
            orgId, siteId, documentId,
            PurchaseDocumentType.Invoice,
            DocumentSource.Upload,
            "/storage/test.pdf",
            "test.pdf",
            "application/pdf",
            1024));

        // Act
        var act = () => grain.ReceiveAsync(new ReceivePurchaseDocumentCommand(
            orgId, siteId, documentId,
            PurchaseDocumentType.Invoice,
            DocumentSource.Upload,
            "/storage/test2.pdf",
            "test2.pdf",
            "application/pdf",
            1024));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Document already exists");
    }

    [Fact]
    public async Task ApplyExtractionResultAsync_ShouldPopulateExtractedData()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, documentId);

        await grain.ReceiveAsync(new ReceivePurchaseDocumentCommand(
            orgId, siteId, documentId,
            PurchaseDocumentType.Invoice,
            DocumentSource.Email,
            "/storage/test.pdf",
            "invoice.pdf",
            "application/pdf",
            1024));

        await grain.RequestProcessingAsync();

        var extractedData = new ExtractedDocumentData
        {
            DetectedType = PurchaseDocumentType.Invoice,
            VendorName = "Acme Foods Inc.",
            VendorAddress = "123 Supplier St",
            InvoiceNumber = "INV-001",
            DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Lines = new List<ExtractedLineItem>
            {
                new ExtractedLineItem
                {
                    Description = "Chicken Breast 5kg",
                    Quantity = 2,
                    Unit = "case",
                    UnitPrice = 45.00m,
                    TotalPrice = 90.00m,
                    Confidence = 0.95m
                }
            },
            Subtotal = 90.00m,
            Tax = 7.20m,
            Total = 97.20m,
            Currency = "USD"
        };

        // Act
        await grain.ApplyExtractionResultAsync(new ApplyExtractionResultCommand(
            extractedData, 0.92m, "azure-v1"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PurchaseDocumentStatus.Extracted);
        snapshot.VendorName.Should().Be("Acme Foods Inc.");
        snapshot.InvoiceNumber.Should().Be("INV-001");
        snapshot.Total.Should().Be(97.20m);
        snapshot.ExtractionConfidence.Should().Be(0.92m);
        snapshot.Lines.Should().HaveCount(1);
        snapshot.Lines[0].Description.Should().Be("Chicken Breast 5kg");
        snapshot.Lines[0].Quantity.Should().Be(2);
    }

    [Fact]
    public async Task MapLineAsync_ShouldUpdateLineMapping()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, documentId);

        await grain.ReceiveAsync(new ReceivePurchaseDocumentCommand(
            orgId, siteId, documentId,
            PurchaseDocumentType.Invoice,
            DocumentSource.Upload,
            "/storage/test.pdf",
            "test.pdf",
            "application/pdf",
            1024));

        await grain.RequestProcessingAsync();

        var extractedData = new ExtractedDocumentData
        {
            DetectedType = PurchaseDocumentType.Invoice,
            VendorName = "Test Supplier",
            Lines = new List<ExtractedLineItem>
            {
                new ExtractedLineItem { Description = "CHKN BRST 5KG", Quantity = 1, UnitPrice = 45m, TotalPrice = 45m, Confidence = 0.9m }
            },
            Total = 45m
        };

        await grain.ApplyExtractionResultAsync(new ApplyExtractionResultCommand(extractedData, 0.9m, "v1"));

        // Act
        await grain.MapLineAsync(new MapLineCommand(
            0, ingredientId, "chicken-breast", "Chicken Breast", MappingSource.Manual, 1.0m));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines[0].MappedIngredientId.Should().Be(ingredientId);
        snapshot.Lines[0].MappedIngredientSku.Should().Be("chicken-breast");
        snapshot.Lines[0].MappingSource.Should().Be(MappingSource.Manual);
        snapshot.Lines[0].MappingConfidence.Should().Be(1.0m);
    }

    [Fact]
    public async Task ConfirmAsync_ShouldUpdateStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, documentId);

        await grain.ReceiveAsync(new ReceivePurchaseDocumentCommand(
            orgId, siteId, documentId,
            PurchaseDocumentType.Invoice,
            DocumentSource.Upload,
            "/storage/test.pdf",
            "test.pdf",
            "application/pdf",
            1024));

        await grain.RequestProcessingAsync();

        var extractedData = new ExtractedDocumentData
        {
            DetectedType = PurchaseDocumentType.Invoice,
            VendorName = "Test Supplier",
            Lines = new List<ExtractedLineItem>
            {
                new ExtractedLineItem { Description = "Test Item", Quantity = 1, UnitPrice = 10m, TotalPrice = 10m, Confidence = 0.9m }
            },
            Total = 10m
        };

        await grain.ApplyExtractionResultAsync(new ApplyExtractionResultCommand(extractedData, 0.9m, "v1"));

        // Act
        var snapshot = await grain.ConfirmAsync(new ConfirmPurchaseDocumentCommand(userId));

        // Assert
        snapshot.Status.Should().Be(PurchaseDocumentStatus.Confirmed);
        snapshot.ConfirmedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RejectAsync_ShouldUpdateStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, documentId);

        await grain.ReceiveAsync(new ReceivePurchaseDocumentCommand(
            orgId, siteId, documentId,
            PurchaseDocumentType.Invoice,
            DocumentSource.Upload,
            "/storage/test.pdf",
            "test.pdf",
            "application/pdf",
            1024));

        // Act
        await grain.RejectAsync(new RejectPurchaseDocumentCommand(userId, "Duplicate document"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(PurchaseDocumentStatus.Rejected);
        state.RejectedBy.Should().Be(userId);
        state.RejectionReason.Should().Be("Duplicate document");
    }

    [Fact]
    public async Task MarkExtractionFailedAsync_ShouldRecordError()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, documentId);

        await grain.ReceiveAsync(new ReceivePurchaseDocumentCommand(
            orgId, siteId, documentId,
            PurchaseDocumentType.Receipt,
            DocumentSource.Photo,
            "/storage/test.jpg",
            "blurry-receipt.jpg",
            "image/jpeg",
            512000));

        await grain.RequestProcessingAsync();

        // Act
        await grain.MarkExtractionFailedAsync(new MarkExtractionFailedCommand(
            "Unable to read document", "OCR confidence too low"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PurchaseDocumentStatus.Failed);
        snapshot.ProcessingError.Should().Contain("Unable to read document");
        snapshot.ProcessingError.Should().Contain("OCR confidence too low");
    }

    [Fact]
    public async Task UpdateLineAsync_ShouldCorrectExtractedValues()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, documentId);

        await grain.ReceiveAsync(new ReceivePurchaseDocumentCommand(
            orgId, siteId, documentId,
            PurchaseDocumentType.Invoice,
            DocumentSource.Upload,
            "/storage/test.pdf",
            "test.pdf",
            "application/pdf",
            1024));

        await grain.RequestProcessingAsync();

        var extractedData = new ExtractedDocumentData
        {
            DetectedType = PurchaseDocumentType.Invoice,
            VendorName = "Test Supplier",
            Lines = new List<ExtractedLineItem>
            {
                new ExtractedLineItem { Description = "Chcken Breast", Quantity = 1, UnitPrice = 45m, TotalPrice = 45m, Confidence = 0.7m }
            },
            Total = 45m
        };

        await grain.ApplyExtractionResultAsync(new ApplyExtractionResultCommand(extractedData, 0.7m, "v1"));

        // Act - correct the typo and quantity
        await grain.UpdateLineAsync(new UpdatePurchaseLineCommand(0, "Chicken Breast", 2, "case", 45m));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines[0].Description.Should().Be("Chicken Breast");
        snapshot.Lines[0].Quantity.Should().Be(2);
        snapshot.Lines[0].TotalPrice.Should().Be(90m); // 2 * 45
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnCorrectValue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, documentId);

        // Assert - should not exist initially
        (await grain.ExistsAsync()).Should().BeFalse();

        // Act
        await grain.ReceiveAsync(new ReceivePurchaseDocumentCommand(
            orgId, siteId, documentId,
            PurchaseDocumentType.Invoice,
            DocumentSource.Upload,
            "/storage/test.pdf",
            "test.pdf",
            "application/pdf",
            1024));

        // Assert - should exist after receive
        (await grain.ExistsAsync()).Should().BeTrue();
    }
}
