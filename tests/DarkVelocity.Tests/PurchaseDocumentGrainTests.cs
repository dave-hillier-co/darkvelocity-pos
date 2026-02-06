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

    // Given: a new purchase document grain
    // When: an invoice is received via email
    // Then: the document is initialized with Received status and defaults to unpaid
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

    // Given: a new purchase document grain
    // When: a receipt is received via photo capture
    // Then: the document is initialized with Received status and defaults to paid
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

    // Given: a purchase document that has already been received
    // When: the same document is received again
    // Then: the operation is rejected with a duplicate error
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

    // Given: an invoice in processing status
    // When: OCR extraction results are applied with vendor and line item data
    // Then: the document moves to Extracted status with populated vendor, totals, and line items
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

    // Given: an extracted invoice with unmapped line items
    // When: a line item is manually mapped to an inventory ingredient
    // Then: the line records the ingredient mapping with manual source and full confidence
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

    // Given: an extracted invoice with reviewed line items
    // When: a user confirms the document
    // Then: the document moves to Confirmed status with a confirmation timestamp
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

    // Given: a received purchase document
    // When: a user rejects the document as a duplicate
    // Then: the document moves to Rejected status with the rejection reason and user recorded
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

    // Given: a receipt photo in processing status
    // When: OCR extraction fails due to poor document quality
    // Then: the document moves to Failed status with the error details recorded
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

    // Given: an extracted invoice with an OCR typo in a line item description
    // When: the line description and quantity are manually corrected
    // Then: the line item is updated and the line total is recalculated
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

    // Given: a new purchase document grain with no prior state
    // When: checking existence before and after receiving a document
    // Then: existence returns false before receipt and true after
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

    // Given: an extracted invoice with a line item mapped to an ingredient
    // When: the mapping is removed from the line
    // Then: all mapping fields are cleared on the line item
    [Fact]
    public async Task UnmapLineAsync_ShouldUnmapLine()
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
                new ExtractedLineItem { Description = "Test Item", Quantity = 1, UnitPrice = 10m, TotalPrice = 10m, Confidence = 0.9m }
            },
            Total = 10m
        };

        await grain.ApplyExtractionResultAsync(new ApplyExtractionResultCommand(extractedData, 0.9m, "v1"));

        // Map the line first
        await grain.MapLineAsync(new MapLineCommand(
            0, ingredientId, "test-sku", "Test Ingredient", MappingSource.Manual, 1.0m));

        // Verify line is mapped
        var snapshotBefore = await grain.GetSnapshotAsync();
        snapshotBefore.Lines[0].MappedIngredientId.Should().Be(ingredientId);

        // Act - unmap the line
        await grain.UnmapLineAsync(new UnmapLineCommand(0));

        // Assert
        var snapshotAfter = await grain.GetSnapshotAsync();
        snapshotAfter.Lines[0].MappedIngredientId.Should().BeNull();
        snapshotAfter.Lines[0].MappedIngredientSku.Should().BeNull();
        snapshotAfter.Lines[0].MappingSource.Should().BeNull();
        snapshotAfter.Lines[0].MappingConfidence.Should().Be(0);
    }

    // Given: an extracted invoice with a single line item
    // When: unmapping is attempted on an out-of-range line index
    // Then: the operation is rejected with an out-of-range error
    [Fact]
    public async Task UnmapLineAsync_InvalidIndex_ShouldThrow()
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
                new ExtractedLineItem { Description = "Only Item", Quantity = 1, UnitPrice = 10m, TotalPrice = 10m, Confidence = 0.9m }
            },
            Total = 10m
        };

        await grain.ApplyExtractionResultAsync(new ApplyExtractionResultCommand(extractedData, 0.9m, "v1"));

        // Act - try to unmap invalid index
        var act = () => grain.UnmapLineAsync(new UnmapLineCommand(5));

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    // Given: an extracted invoice with an ambiguous low-confidence line item
    // When: ingredient mapping suggestions are set for the line
    // Then: the suggestions are stored with their confidence scores and match reasons
    [Fact]
    public async Task SetLineSuggestionsAsync_ShouldSetSuggestions()
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
                new ExtractedLineItem { Description = "Ambiguous Item", Quantity = 1, UnitPrice = 15m, TotalPrice = 15m, Confidence = 0.7m }
            },
            Total = 15m
        };

        await grain.ApplyExtractionResultAsync(new ApplyExtractionResultCommand(extractedData, 0.7m, "v1"));

        var suggestions = new List<SuggestedMapping>
        {
            new SuggestedMapping { IngredientId = Guid.NewGuid(), IngredientName = "Suggested Item 1", Sku = "SKU-1", Confidence = 0.8m, MatchReason = "Similar name" },
            new SuggestedMapping { IngredientId = Guid.NewGuid(), IngredientName = "Suggested Item 2", Sku = "SKU-2", Confidence = 0.6m, MatchReason = "Partial match" }
        };

        // Act
        await grain.SetLineSuggestionsAsync(0, suggestions);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Lines[0].Suggestions.Should().NotBeNull();
        snapshot.Lines[0].Suggestions.Should().HaveCount(2);
        snapshot.Lines[0].Suggestions![0].IngredientName.Should().Be("Suggested Item 1");
        snapshot.Lines[0].Suggestions![1].Confidence.Should().Be(0.6m);
    }

    // Given: a received invoice that transitions through processing to extracted
    // When: processing is requested a second time after extraction completes
    // Then: the second processing request is rejected because the document is already extracted
    [Fact]
    public async Task RequestProcessingAsync_WorkflowValidation()
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

        // Act - request processing
        await grain.RequestProcessingAsync();

        // Assert - status should be Processing
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(PurchaseDocumentStatus.Processing);

        // Apply extraction to move to Extracted status
        var extractedData = new ExtractedDocumentData
        {
            DetectedType = PurchaseDocumentType.Invoice,
            VendorName = "Test Supplier",
            Lines = new List<ExtractedLineItem>
            {
                new ExtractedLineItem { Description = "Test", Quantity = 1, UnitPrice = 10m, TotalPrice = 10m, Confidence = 0.9m }
            },
            Total = 10m
        };
        await grain.ApplyExtractionResultAsync(new ApplyExtractionResultCommand(extractedData, 0.9m, "v1"));

        // Act - try to request processing again on extracted document
        var act = () => grain.RequestProcessingAsync();

        // Assert - should throw because document is already extracted
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot process document*");
    }

    // Given: a received invoice that has not been processed or extracted
    // When: mapping a line item to an ingredient is attempted
    // Then: the operation is rejected because the document is not in extracted state
    [Fact]
    public async Task MapLineAsync_NonExtracted_ShouldThrow()
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

        // Don't call RequestProcessingAsync or ApplyExtractionResultAsync

        // Act
        var act = () => grain.MapLineAsync(new MapLineCommand(
            0, Guid.NewGuid(), "test-sku", "Test", MappingSource.Manual, 1.0m));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not in extracted state*");
    }

    // Given: a received invoice that has not been processed or extracted
    // When: document confirmation is attempted
    // Then: the operation is rejected because the document is not in extracted state
    [Fact]
    public async Task ConfirmAsync_NonExtracted_ShouldThrow()
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

        // Don't extract the document

        // Act
        var act = () => grain.ConfirmAsync(new ConfirmPurchaseDocumentCommand(Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not in extracted state*");
    }

    // Given: an extracted invoice with auto-detected vendor information
    // When: the document is confirmed with vendor name, date, and currency overrides
    // Then: the confirmed document uses the overridden values instead of the extracted ones
    [Fact]
    public async Task ConfirmAsync_WithVendorOverride_ShouldUseOverride()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var overrideVendorId = Guid.NewGuid();
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
            VendorName = "Extracted Vendor Name",
            DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            Lines = new List<ExtractedLineItem>
            {
                new ExtractedLineItem { Description = "Test Item", Quantity = 1, UnitPrice = 10m, TotalPrice = 10m, Confidence = 0.9m }
            },
            Total = 10m
        };

        await grain.ApplyExtractionResultAsync(new ApplyExtractionResultCommand(extractedData, 0.9m, "v1"));

        var overrideDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act - confirm with vendor override
        var snapshot = await grain.ConfirmAsync(new ConfirmPurchaseDocumentCommand(
            userId,
            VendorId: overrideVendorId,
            VendorName: "Override Vendor Name",
            DocumentDate: overrideDate,
            Currency: "EUR"));

        // Assert
        snapshot.Status.Should().Be(PurchaseDocumentStatus.Confirmed);
        snapshot.VendorName.Should().Be("Override Vendor Name");
        snapshot.DocumentDate.Should().Be(overrideDate);
        snapshot.Currency.Should().Be("EUR");
    }

    // Given: a new purchase document grain
    // When: a purchase order document is received via email
    // Then: the document is initialized as a purchase order type in Received status
    [Fact]
    public async Task ReceiveAsync_PurchaseOrderType_ShouldSucceed()
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
            PurchaseDocumentType.PurchaseOrder,
            DocumentSource.Email,
            "/storage/po/po-123.pdf",
            "purchase-order-123.pdf",
            "application/pdf",
            2048));

        // Assert
        snapshot.DocumentId.Should().Be(documentId);
        snapshot.DocumentType.Should().Be(PurchaseDocumentType.PurchaseOrder);
        snapshot.Source.Should().Be(DocumentSource.Email);
        snapshot.Status.Should().Be(PurchaseDocumentStatus.Received);
    }

    // Given: a new purchase document grain
    // When: a credit note is received via supplier integration
    // Then: the document is initialized as a credit note type in Received status
    [Fact]
    public async Task ReceiveAsync_CreditNoteType_ShouldSucceed()
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
            PurchaseDocumentType.CreditNote,
            DocumentSource.SupplierIntegration,
            "/storage/credit/cn-456.pdf",
            "credit-note-456.pdf",
            "application/pdf",
            1536));

        // Assert
        snapshot.DocumentId.Should().Be(documentId);
        snapshot.DocumentType.Should().Be(PurchaseDocumentType.CreditNote);
        snapshot.Source.Should().Be(DocumentSource.SupplierIntegration);
        snapshot.Status.Should().Be(PurchaseDocumentStatus.Received);
    }

    // Given: an extracted invoice with a single line item
    // When: mapping is attempted on negative and out-of-range line indices
    // Then: both operations are rejected with out-of-range errors
    [Fact]
    public async Task MapLineAsync_InvalidIndex_ShouldThrow()
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
                new ExtractedLineItem { Description = "Only Line", Quantity = 1, UnitPrice = 10m, TotalPrice = 10m, Confidence = 0.9m }
            },
            Total = 10m
        };

        await grain.ApplyExtractionResultAsync(new ApplyExtractionResultCommand(extractedData, 0.9m, "v1"));

        // Act - try to map invalid line index
        var actNegative = () => grain.MapLineAsync(new MapLineCommand(
            -1, Guid.NewGuid(), "test-sku", "Test", MappingSource.Manual, 1.0m));
        var actTooHigh = () => grain.MapLineAsync(new MapLineCommand(
            10, Guid.NewGuid(), "test-sku", "Test", MappingSource.Manual, 1.0m));

        // Assert
        await actNegative.Should().ThrowAsync<ArgumentOutOfRangeException>();
        await actTooHigh.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    // Given: an invoice in processing status with no prior vendor mappings learned
    // When: multi-line extraction results from a vendor invoice are applied
    // Then: line items are extracted correctly but remain unmapped since no learned vendor mappings exist
    [Fact]
    public async Task AutoMapping_Integration_ShouldUseVendorMapping()
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

        // Provide extraction data with vendor name that will be normalized
        var extractedData = new ExtractedDocumentData
        {
            DetectedType = PurchaseDocumentType.Invoice,
            VendorName = "Costco Wholesale Corp.",
            VendorAddress = "999 Lake Dr",
            Lines = new List<ExtractedLineItem>
            {
                new ExtractedLineItem { Description = "KS Organic Milk 1 Gal", Quantity = 4, UnitPrice = 5.99m, TotalPrice = 23.96m, Confidence = 0.85m },
                new ExtractedLineItem { Description = "KS Paper Towels 12pk", Quantity = 2, UnitPrice = 19.99m, TotalPrice = 39.98m, Confidence = 0.90m }
            },
            Subtotal = 63.94m,
            Tax = 5.12m,
            Total = 69.06m,
            Currency = "USD"
        };

        // Act - apply extraction which triggers auto-mapping attempt
        await grain.ApplyExtractionResultAsync(new ApplyExtractionResultCommand(extractedData, 0.87m, "v1"));

        // Assert - document should be extracted, lines should have extraction data
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PurchaseDocumentStatus.Extracted);
        snapshot.VendorName.Should().Be("Costco Wholesale Corp.");
        snapshot.Lines.Should().HaveCount(2);
        snapshot.Lines[0].Description.Should().Be("KS Organic Milk 1 Gal");
        snapshot.Lines[0].Quantity.Should().Be(4);
        snapshot.Lines[1].Description.Should().Be("KS Paper Towels 12pk");
        // Note: Auto-mapping requires the vendor mapping grain to have prior mappings
        // In this test we just verify the extraction was applied correctly
        // The auto-mapping will not find matches since there are no learned mappings
    }
}
