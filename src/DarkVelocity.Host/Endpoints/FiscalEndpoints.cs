using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DarkVelocity.Host.Endpoints;

public static class FiscalEndpoints
{
    public static WebApplication MapFiscalEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}")
            .WithTags("Fiscal")
            .RequireAuthorization();

        // ========================================================================
        // Fiscal Devices
        // ========================================================================

        group.MapGet("/fiscal-devices", async (
            Guid orgId,
            Guid siteId,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var registryGrain = grainFactory.GetGrain<IFiscalDeviceRegistryGrain>(
                GrainKeys.FiscalDeviceRegistry(orgId, siteId));

            var deviceIds = await registryGrain.GetDeviceIdsAsync();
            var devices = new List<FiscalDeviceResponse>();

            foreach (var deviceId in deviceIds)
            {
                var deviceGrain = grainFactory.GetGrain<IFiscalDeviceGrain>(
                    GrainKeys.FiscalDevice(orgId, siteId, deviceId));
                try
                {
                    var snapshot = await deviceGrain.GetSnapshotAsync();
                    devices.Add(ToResponse(snapshot, siteId));
                }
                catch
                {
                    // Device may have been deleted
                }
            }

            return Results.Ok(new FiscalDeviceListResponse(devices, devices.Count));
        }).WithName("ListFiscalDevices")
          .WithDescription("List all fiscal devices for a site");

        group.MapGet("/fiscal-devices/{deviceId}", async (
            Guid orgId,
            Guid siteId,
            Guid deviceId,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var deviceGrain = grainFactory.GetGrain<IFiscalDeviceGrain>(
                GrainKeys.FiscalDevice(orgId, siteId, deviceId));

            try
            {
                var snapshot = await deviceGrain.GetSnapshotAsync();
                return Results.Ok(ToResponse(snapshot, siteId));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(new { error = "not_found", error_description = "Fiscal device not found" });
            }
        }).WithName("GetFiscalDevice")
          .WithDescription("Get a specific fiscal device");

        group.MapPost("/fiscal-devices", async (
            Guid orgId,
            Guid siteId,
            [FromBody] RegisterFiscalDeviceRequest request,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var deviceId = Guid.NewGuid();
            var deviceGrain = grainFactory.GetGrain<IFiscalDeviceGrain>(
                GrainKeys.FiscalDevice(orgId, siteId, deviceId));

            if (!Enum.TryParse<FiscalDeviceType>(request.DeviceType, true, out var deviceType))
                return Results.BadRequest(new { error = "invalid_device_type", error_description = "Invalid device type" });

            var command = new RegisterFiscalDeviceCommand(
                LocationId: siteId,
                DeviceType: deviceType,
                SerialNumber: request.SerialNumber,
                PublicKey: request.PublicKey,
                CertificateExpiryDate: request.CertificateExpiryDate,
                ApiEndpoint: request.ApiEndpoint,
                ApiCredentialsEncrypted: request.ApiCredentialsEncrypted,
                ClientId: request.ClientId);

            var snapshot = await deviceGrain.RegisterAsync(command);

            // Register with the registry
            var registryGrain = grainFactory.GetGrain<IFiscalDeviceRegistryGrain>(
                GrainKeys.FiscalDeviceRegistry(orgId, siteId));
            await registryGrain.RegisterDeviceAsync(deviceId, request.SerialNumber);

            return Results.Created(
                $"/api/orgs/{orgId}/sites/{siteId}/fiscal-devices/{deviceId}",
                ToResponse(snapshot, siteId));
        }).WithName("RegisterFiscalDevice")
          .WithDescription("Register a new fiscal device");

        group.MapPut("/fiscal-devices/{deviceId}", async (
            Guid orgId,
            Guid siteId,
            Guid deviceId,
            [FromBody] UpdateFiscalDeviceRequest request,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var deviceGrain = grainFactory.GetGrain<IFiscalDeviceGrain>(
                GrainKeys.FiscalDevice(orgId, siteId, deviceId));

            FiscalDeviceStatus? status = null;
            if (request.Status != null && Enum.TryParse<FiscalDeviceStatus>(request.Status, true, out var parsedStatus))
                status = parsedStatus;

            try
            {
                var command = new UpdateFiscalDeviceCommand(
                    Status: status,
                    PublicKey: request.PublicKey,
                    CertificateExpiryDate: request.CertificateExpiryDate,
                    ApiEndpoint: request.ApiEndpoint,
                    ApiCredentialsEncrypted: request.ApiCredentialsEncrypted);

                var snapshot = await deviceGrain.UpdateAsync(command);
                return Results.Ok(ToResponse(snapshot, siteId));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(new { error = "not_found", error_description = "Fiscal device not found" });
            }
        }).WithName("UpdateFiscalDevice")
          .WithDescription("Update a fiscal device");

        group.MapPost("/fiscal-devices/{deviceId}/activate", async (
            Guid orgId,
            Guid siteId,
            Guid deviceId,
            [FromBody] ActivateFiscalDeviceRequest request,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var deviceGrain = grainFactory.GetGrain<IFiscalDeviceGrain>(
                GrainKeys.FiscalDevice(orgId, siteId, deviceId));

            try
            {
                var snapshot = await deviceGrain.ActivateAsync(
                    request.TaxAuthorityRegistrationId,
                    request.OperatorId ?? GetUserId(user) ?? Guid.Empty);
                return Results.Ok(ToResponse(snapshot, siteId));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = "activation_failed", error_description = ex.Message });
            }
        }).WithName("ActivateFiscalDevice")
          .WithDescription("Activate a fiscal device (register with tax authority)");

        group.MapPost("/fiscal-devices/{deviceId}/deactivate", async (
            Guid orgId,
            Guid siteId,
            Guid deviceId,
            [FromBody] DeactivateFiscalDeviceRequest request,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var deviceGrain = grainFactory.GetGrain<IFiscalDeviceGrain>(
                GrainKeys.FiscalDevice(orgId, siteId, deviceId));

            try
            {
                await deviceGrain.DeactivateWithReasonAsync(
                    request.Reason,
                    request.OperatorId ?? GetUserId(user) ?? Guid.Empty);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = "deactivation_failed", error_description = ex.Message });
            }
        }).WithName("DeactivateFiscalDevice")
          .WithDescription("Deactivate a fiscal device");

        group.MapGet("/fiscal-devices/{deviceId}/health", async (
            Guid orgId,
            Guid siteId,
            Guid deviceId,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var deviceGrain = grainFactory.GetGrain<IFiscalDeviceGrain>(
                GrainKeys.FiscalDevice(orgId, siteId, deviceId));

            try
            {
                var health = await deviceGrain.GetHealthStatusAsync();
                return Results.Ok(health);
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(new { error = "not_found", error_description = "Fiscal device not found" });
            }
        }).WithName("GetFiscalDeviceHealth")
          .WithDescription("Get health status of a fiscal device");

        group.MapPost("/fiscal-devices/{deviceId}/self-test", async (
            Guid orgId,
            Guid siteId,
            Guid deviceId,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var deviceGrain = grainFactory.GetGrain<IFiscalDeviceGrain>(
                GrainKeys.FiscalDevice(orgId, siteId, deviceId));

            try
            {
                var result = await deviceGrain.PerformSelfTestAsync();
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = "self_test_failed", error_description = ex.Message });
            }
        }).WithName("FiscalDeviceSelfTest")
          .WithDescription("Perform self-test on a fiscal device");

        // ========================================================================
        // Fiscal Transactions
        // ========================================================================

        group.MapGet("/fiscal-transactions", async (
            Guid orgId,
            Guid siteId,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] Guid? deviceId,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 20 : pageSize > 100 ? 100 : pageSize;

            var registryGrain = grainFactory.GetGrain<IFiscalTransactionRegistryGrain>(
                GrainKeys.FiscalTransactionRegistry(orgId, siteId));

            var transactionIds = await registryGrain.GetTransactionIdsAsync(
                startDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
                endDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                deviceId);

            var totalCount = transactionIds.Count;
            var pagedIds = transactionIds
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var transactions = new List<FiscalTransactionResponse>();

            foreach (var txId in pagedIds)
            {
                var txGrain = grainFactory.GetGrain<IFiscalTransactionGrain>(
                    GrainKeys.FiscalTransaction(orgId, siteId, txId));
                try
                {
                    var snapshot = await txGrain.GetSnapshotAsync();
                    transactions.Add(ToResponse(snapshot, siteId));
                }
                catch
                {
                    // Transaction may have been deleted
                }
            }

            return Results.Ok(new FiscalTransactionListResponse(
                transactions, totalCount, page, pageSize));
        }).WithName("ListFiscalTransactions")
          .WithDescription("List fiscal transactions with pagination and filtering");

        group.MapGet("/fiscal-transactions/{transactionId}", async (
            Guid orgId,
            Guid siteId,
            Guid transactionId,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var txGrain = grainFactory.GetGrain<IFiscalTransactionGrain>(
                GrainKeys.FiscalTransaction(orgId, siteId, transactionId));

            try
            {
                var snapshot = await txGrain.GetSnapshotAsync();
                return Results.Ok(ToResponse(snapshot, siteId));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(new { error = "not_found", error_description = "Fiscal transaction not found" });
            }
        }).WithName("GetFiscalTransaction")
          .WithDescription("Get a specific fiscal transaction");

        group.MapPost("/fiscal-transactions", async (
            Guid orgId,
            Guid siteId,
            [FromBody] CreateFiscalTransactionRequest request,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            if (!Enum.TryParse<FiscalTransactionType>(request.TransactionType, true, out var txType))
                return Results.BadRequest(new { error = "invalid_transaction_type", error_description = "Invalid transaction type" });

            if (!Enum.TryParse<FiscalProcessType>(request.ProcessType, true, out var processType))
                return Results.BadRequest(new { error = "invalid_process_type", error_description = "Invalid process type" });

            var transactionId = Guid.NewGuid();
            var txGrain = grainFactory.GetGrain<IFiscalTransactionGrain>(
                GrainKeys.FiscalTransaction(orgId, siteId, transactionId));

            var command = new CreateFiscalTransactionCommand(
                FiscalDeviceId: request.FiscalDeviceId,
                LocationId: siteId,
                TransactionType: txType,
                ProcessType: processType,
                SourceType: request.SourceType,
                SourceId: request.SourceId,
                GrossAmount: request.GrossAmount,
                NetAmounts: request.NetAmounts,
                TaxAmounts: request.TaxAmounts,
                PaymentTypes: request.PaymentTypes);

            var snapshot = await txGrain.CreateAsync(command);

            // Register with the transaction registry
            var registryGrain = grainFactory.GetGrain<IFiscalTransactionRegistryGrain>(
                GrainKeys.FiscalTransactionRegistry(orgId, siteId));
            await registryGrain.RegisterTransactionAsync(
                transactionId,
                request.FiscalDeviceId,
                DateOnly.FromDateTime(snapshot.StartTime));

            return Results.Created(
                $"/api/orgs/{orgId}/sites/{siteId}/fiscal-transactions/{transactionId}",
                ToResponse(snapshot, siteId));
        }).WithName("CreateFiscalTransaction")
          .WithDescription("Create a new fiscal transaction");

        // ========================================================================
        // Fiscal Journal
        // ========================================================================

        group.MapGet("/fiscal-journal", async (
            Guid orgId,
            Guid siteId,
            [FromQuery] DateOnly? date,
            [FromQuery] Guid? deviceId,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var journalGrain = grainFactory.GetGrain<IFiscalJournalGrain>(
                GrainKeys.FiscalJournal(orgId, siteId, targetDate));

            IReadOnlyList<FiscalJournalEntry> entries;
            if (deviceId.HasValue)
            {
                entries = await journalGrain.GetEntriesByDeviceAsync(deviceId.Value);
            }
            else
            {
                entries = await journalGrain.GetEntriesAsync();
            }

            var response = new FiscalJournalResponse(
                entries.Select(e => new FiscalJournalEntryResponse(
                    e.EntryId,
                    e.Timestamp,
                    e.LocationId,
                    e.EventType.ToString(),
                    e.DeviceId,
                    e.TransactionId,
                    e.ExportId,
                    e.Details,
                    e.IpAddress,
                    e.UserId,
                    e.Severity.ToString())).ToList(),
                entries.Count,
                targetDate);

            return Results.Ok(response);
        }).WithName("GetFiscalJournal")
          .WithDescription("Get fiscal journal entries for a date");

        group.MapGet("/fiscal-journal/export", async (
            Guid orgId,
            Guid siteId,
            [FromQuery] DateOnly startDate,
            [FromQuery] DateOnly endDate,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var allEntries = new List<FiscalJournalEntry>();

            for (var d = startDate; d <= endDate; d = d.AddDays(1))
            {
                var journalGrain = grainFactory.GetGrain<IFiscalJournalGrain>(
                    GrainKeys.FiscalJournal(orgId, siteId, d));
                var dayEntries = await journalGrain.GetEntriesAsync();
                allEntries.AddRange(dayEntries);
            }

            var response = allEntries.Select(e => new FiscalJournalEntryResponse(
                e.EntryId,
                e.Timestamp,
                e.LocationId,
                e.EventType.ToString(),
                e.DeviceId,
                e.TransactionId,
                e.ExportId,
                e.Details,
                e.IpAddress,
                e.UserId,
                e.Severity.ToString())).ToList();

            return Results.Ok(new { entries = response, total = response.Count, startDate, endDate });
        }).WithName("ExportFiscalJournal")
          .WithDescription("Export fiscal journal entries for a date range");

        // ========================================================================
        // DSFinV-K Export (German Tax Audit Format)
        // ========================================================================

        group.MapPost("/fiscal/dsfinvk-export", async (
            Guid orgId,
            Guid siteId,
            [FromBody] GenerateDSFinVKExportRequest request,
            ClaimsPrincipal user,
            IGrainFactory grainFactory,
            IDSFinVKExportService exportService) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var exportId = Guid.NewGuid();
            var userId = GetUserId(user);

            var result = await exportService.GenerateExportAsync(
                orgId,
                siteId,
                exportId,
                request.StartDate,
                request.EndDate,
                request.Description,
                request.DeviceIds,
                userId);

            return Results.Accepted(
                $"/api/orgs/{orgId}/sites/{siteId}/fiscal/dsfinvk-export/{exportId}",
                result);
        }).WithName("GenerateDSFinVKExport")
          .WithDescription("Generate a DSFinV-K export for German tax audits");

        group.MapGet("/fiscal/dsfinvk-export/{exportId}", async (
            Guid orgId,
            Guid siteId,
            Guid exportId,
            ClaimsPrincipal user,
            IGrainFactory grainFactory,
            IDSFinVKExportService exportService) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var result = await exportService.GetExportStatusAsync(orgId, siteId, exportId);

            if (result == null)
                return Results.NotFound(new { error = "not_found", error_description = "Export not found" });

            return Results.Ok(result);
        }).WithName("GetDSFinVKExportStatus")
          .WithDescription("Get status of a DSFinV-K export");

        group.MapGet("/fiscal/dsfinvk-export/{exportId}/download", async (
            Guid orgId,
            Guid siteId,
            Guid exportId,
            ClaimsPrincipal user,
            IDSFinVKExportService exportService) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var (stream, fileName) = await exportService.DownloadExportAsync(orgId, siteId, exportId);

            if (stream == null)
                return Results.NotFound(new { error = "not_found", error_description = "Export not found or not ready" });

            return Results.File(stream, "application/zip", fileName);
        }).WithName("DownloadDSFinVKExport")
          .WithDescription("Download a completed DSFinV-K export");

        group.MapGet("/fiscal/dsfinvk-exports", async (
            Guid orgId,
            Guid siteId,
            ClaimsPrincipal user,
            IDSFinVKExportService exportService) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var exports = await exportService.ListExportsAsync(orgId, siteId);
            return Results.Ok(new DSFinVKExportListResponse(exports, exports.Count));
        }).WithName("ListDSFinVKExports")
          .WithDescription("List all DSFinV-K exports for a site");

        // ========================================================================
        // Multi-Country Fiscal Configuration
        // ========================================================================

        group.MapGet("/fiscal/configuration", async (
            Guid orgId,
            Guid siteId,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var fiscalGrain = grainFactory.GetGrain<IMultiCountryFiscalGrain>(
                GrainKeys.MultiCountryFiscal(orgId, siteId));

            var snapshot = await fiscalGrain.GetSnapshotAsync();
            return Results.Ok(snapshot);
        }).WithName("GetFiscalConfiguration")
          .WithDescription("Get multi-country fiscal configuration for a site");

        group.MapPut("/fiscal/configuration", async (
            Guid orgId,
            Guid siteId,
            [FromBody] ConfigureSiteFiscalRequest request,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            if (!Enum.TryParse<FiscalCountry>(request.Country, true, out var country))
                return Results.BadRequest(new { error = "invalid_country", error_description = "Invalid fiscal country" });

            ExternalTseType? tseType = null;
            if (request.TseType != null && Enum.TryParse<ExternalTseType>(request.TseType, true, out var parsedTseType))
                tseType = parsedTseType;

            var fiscalGrain = grainFactory.GetGrain<IMultiCountryFiscalGrain>(
                GrainKeys.MultiCountryFiscal(orgId, siteId));

            var command = new ConfigureSiteFiscalCommand(
                Country: country,
                Enabled: request.Enabled,
                TseDeviceId: request.TseDeviceId,
                TseType: tseType,
                CountrySpecificConfig: request.CountrySpecificConfig);

            var snapshot = await fiscalGrain.ConfigureAsync(command);
            return Results.Ok(snapshot);
        }).WithName("ConfigureFiscal")
          .WithDescription("Configure multi-country fiscal settings for a site");

        group.MapGet("/fiscal/configuration/validate", async (
            Guid orgId,
            Guid siteId,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var fiscalGrain = grainFactory.GetGrain<IMultiCountryFiscalGrain>(
                GrainKeys.MultiCountryFiscal(orgId, siteId));

            var result = await fiscalGrain.ValidateConfigurationAsync();
            return Results.Ok(result);
        }).WithName("ValidateFiscalConfiguration")
          .WithDescription("Validate fiscal configuration for a site");

        group.MapGet("/fiscal/health", async (
            Guid orgId,
            Guid siteId,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var fiscalGrain = grainFactory.GetGrain<IMultiCountryFiscalGrain>(
                GrainKeys.MultiCountryFiscal(orgId, siteId));

            var health = await fiscalGrain.GetHealthStatusAsync();
            return Results.Ok(health);
        }).WithName("GetFiscalHealth")
          .WithDescription("Get fiscal service health status");

        group.MapGet("/fiscal/features", async (
            Guid orgId,
            Guid siteId,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var fiscalGrain = grainFactory.GetGrain<IMultiCountryFiscalGrain>(
                GrainKeys.MultiCountryFiscal(orgId, siteId));

            var features = await fiscalGrain.GetSupportedFeaturesAsync();
            return Results.Ok(new { features = features.Select(f => f.ToString()).ToList() });
        }).WithName("GetFiscalFeatures")
          .WithDescription("Get supported fiscal features for configured country");

        // ========================================================================
        // Country-Specific Export Endpoints
        // ========================================================================

        group.MapPost("/fiscal/export", async (
            Guid orgId,
            Guid siteId,
            [FromBody] GenerateFiscalExportRequest request,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var fiscalGrain = grainFactory.GetGrain<IMultiCountryFiscalGrain>(
                GrainKeys.MultiCountryFiscal(orgId, siteId));

            try
            {
                var range = new FiscalDateRange(request.StartDate, request.EndDate);
                var exportData = await fiscalGrain.GenerateAuditExportAsync(range);

                var contentType = request.Format?.ToLowerInvariant() switch
                {
                    "xml" => "application/xml",
                    "json" => "application/json",
                    _ => "application/octet-stream"
                };

                var fileName = $"fiscal-export-{siteId:N}-{request.StartDate:yyyyMMdd}-{request.EndDate:yyyyMMdd}.{request.Format ?? "dat"}";
                return Results.File(exportData, contentType, fileName);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "export_failed", error_description = ex.Message });
            }
        }).WithName("GenerateFiscalExport")
          .WithDescription("Generate country-specific fiscal export");

        // ========================================================================
        // Z-Reports
        // ========================================================================

        group.MapPost("/fiscal/z-report", async (
            Guid orgId,
            Guid siteId,
            [FromBody] GenerateZReportRequest request,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var zReportGrain = grainFactory.GetGrain<IZReportGrain>(
                GrainKeys.ZReport(orgId, siteId));

            try
            {
                var report = await zReportGrain.GenerateReportAsync(request.BusinessDate);
                return Results.Created(
                    $"/api/orgs/{orgId}/sites/{siteId}/fiscal/z-report/{report.ReportNumber}",
                    report);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = "z_report_failed", error_description = ex.Message });
            }
        }).WithName("GenerateZReport")
          .WithDescription("Generate Z-report for a business date");

        group.MapGet("/fiscal/z-report/{reportNumber:long}", async (
            Guid orgId,
            Guid siteId,
            long reportNumber,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var zReportGrain = grainFactory.GetGrain<IZReportGrain>(
                GrainKeys.ZReport(orgId, siteId));

            var report = await zReportGrain.GetReportAsync(reportNumber);

            if (report == null)
                return Results.NotFound(new { error = "not_found", error_description = "Z-report not found" });

            return Results.Ok(report);
        }).WithName("GetZReport")
          .WithDescription("Get a specific Z-report");

        group.MapGet("/fiscal/z-reports", async (
            Guid orgId,
            Guid siteId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var zReportGrain = grainFactory.GetGrain<IZReportGrain>(
                GrainKeys.ZReport(orgId, siteId));

            var reports = await zReportGrain.GetReportsAsync(
                startDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
                endDate ?? DateOnly.FromDateTime(DateTime.UtcNow));

            return Results.Ok(new { reports, total = reports.Count });
        }).WithName("ListZReports")
          .WithDescription("List Z-reports for a date range");

        group.MapGet("/fiscal/z-report/latest", async (
            Guid orgId,
            Guid siteId,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var zReportGrain = grainFactory.GetGrain<IZReportGrain>(
                GrainKeys.ZReport(orgId, siteId));

            var report = await zReportGrain.GetLatestReportAsync();

            if (report == null)
                return Results.NotFound(new { error = "not_found", error_description = "No Z-reports found" });

            return Results.Ok(report);
        }).WithName("GetLatestZReport")
          .WithDescription("Get the latest Z-report");

        // ========================================================================
        // Daily Close Operations
        // ========================================================================

        group.MapPost("/fiscal/daily-close", async (
            Guid orgId,
            Guid siteId,
            [FromBody] TriggerDailyCloseRequest request,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var fiscalGrain = grainFactory.GetGrain<IMultiCountryFiscalGrain>(
                GrainKeys.MultiCountryFiscal(orgId, siteId));

            var result = await fiscalGrain.PerformDailyCloseAsync(request.BusinessDate);

            if (result.Success)
                return Results.Ok(result);

            return Results.BadRequest(new { error = result.ErrorCode, error_description = result.ErrorMessage });
        }).WithName("TriggerDailyClose")
          .WithDescription("Trigger daily fiscal close for a business date");

        // ========================================================================
        // Scheduled Jobs Management
        // ========================================================================

        group.MapGet("/fiscal/jobs", async (
            Guid orgId,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var jobSchedulerGrain = grainFactory.GetGrain<IFiscalJobSchedulerGrain>(
                GrainKeys.FiscalJobScheduler(orgId));

            var configs = await jobSchedulerGrain.GetSiteConfigsAsync();
            return Results.Ok(new { siteConfigs = configs, total = configs.Count });
        }).WithName("ListFiscalJobConfigs")
          .WithDescription("List fiscal job configurations for all sites");

        group.MapPut("/fiscal/jobs/{siteId}", async (
            Guid orgId,
            Guid siteId,
            [FromBody] ConfigureFiscalJobsRequest request,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var jobSchedulerGrain = grainFactory.GetGrain<IFiscalJobSchedulerGrain>(
                GrainKeys.FiscalJobScheduler(orgId));

            var config = new SiteFiscalJobConfig(
                SiteId: siteId,
                DailyCloseEnabled: request.DailyCloseEnabled,
                DailyCloseTime: request.DailyCloseTime,
                ArchiveEnabled: request.ArchiveEnabled,
                ArchiveTime: request.ArchiveTime,
                CertificateMonitoringEnabled: request.CertificateMonitoringEnabled,
                CertificateExpiryWarningDays: request.CertificateExpiryWarningDays,
                TimeZoneId: request.TimeZoneId);

            await jobSchedulerGrain.ConfigureSiteJobsAsync(config);
            return Results.Ok(config);
        }).WithName("ConfigureFiscalJobs")
          .WithDescription("Configure fiscal scheduled jobs for a site");

        group.MapGet("/fiscal/jobs/history", async (
            Guid orgId,
            [FromQuery] int limit,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            limit = limit < 1 ? 50 : limit > 500 ? 500 : limit;

            var jobSchedulerGrain = grainFactory.GetGrain<IFiscalJobSchedulerGrain>(
                GrainKeys.FiscalJobScheduler(orgId));

            var history = await jobSchedulerGrain.GetJobHistoryAsync(limit);
            return Results.Ok(new { history, total = history.Count });
        }).WithName("GetFiscalJobHistory")
          .WithDescription("Get fiscal job execution history");

        group.MapGet("/fiscal/certificates/expiry", async (
            Guid orgId,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var jobSchedulerGrain = grainFactory.GetGrain<IFiscalJobSchedulerGrain>(
                GrainKeys.FiscalJobScheduler(orgId));

            var warnings = await jobSchedulerGrain.CheckCertificateExpiryAsync();
            return Results.Ok(new { warnings, total = warnings.Count });
        }).WithName("CheckCertificateExpiry")
          .WithDescription("Check certificate expiry for all sites");

        // ========================================================================
        // France NF 525 Specific Endpoints
        // ========================================================================

        group.MapPost("/fiscal/france/jet-export", async (
            Guid orgId,
            Guid siteId,
            [FromBody] GenerateFiscalExportRequest request,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var fiscalGrain = grainFactory.GetGrain<IMultiCountryFiscalGrain>(
                GrainKeys.MultiCountryFiscal(orgId, siteId));

            try
            {
                var range = new FiscalDateRange(request.StartDate, request.EndDate);
                var exportData = await fiscalGrain.GenerateAuditExportAsync(range);

                var fileName = $"jet-export-{siteId:N}-{request.StartDate:yyyyMMdd}-{request.EndDate:yyyyMMdd}.json";
                return Results.File(exportData, "application/json", fileName);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "jet_export_failed", error_description = ex.Message });
            }
        }).WithName("GenerateJetExport")
          .WithDescription("Generate French JET (Journal Electronique Technique) export");

        // ========================================================================
        // Poland JPK/KSeF Specific Endpoints
        // ========================================================================

        group.MapPost("/fiscal/poland/jpk-export", async (
            Guid orgId,
            Guid siteId,
            [FromBody] GenerateJpkExportRequest request,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var fiscalGrain = grainFactory.GetGrain<IMultiCountryFiscalGrain>(
                GrainKeys.MultiCountryFiscal(orgId, siteId));

            try
            {
                var range = new FiscalDateRange(request.StartDate, request.EndDate);
                var exportData = await fiscalGrain.GenerateAuditExportAsync(range);

                var documentType = request.DocumentType ?? "JPK_VAT";
                var fileName = $"{documentType}-{siteId:N}-{request.StartDate:yyyyMMdd}-{request.EndDate:yyyyMMdd}.xml";
                return Results.File(exportData, "application/xml", fileName);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "jpk_export_failed", error_description = ex.Message });
            }
        }).WithName("GenerateJpkExport")
          .WithDescription("Generate Polish JPK export");

        // ========================================================================
        // TSE Management Endpoints
        // ========================================================================

        group.MapGet("/fiscal/tse/status", async (
            Guid orgId,
            Guid siteId,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var fiscalGrain = grainFactory.GetGrain<IMultiCountryFiscalGrain>(
                GrainKeys.MultiCountryFiscal(orgId, siteId));

            var health = await fiscalGrain.GetHealthStatusAsync();
            return Results.Ok(new
            {
                status = health.Status.ToString(),
                isOnline = health.IsOnline,
                certificateValid = health.CertificateValid,
                daysUntilCertificateExpiry = health.DaysUntilCertificateExpiry,
                lastTransactionAt = health.LastTransactionAt,
                totalTransactions = health.TotalTransactions,
                lastError = health.LastError
            });
        }).WithName("GetTseStatus")
          .WithDescription("Get TSE device status");

        return app;
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var subClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(subClaim) || !Guid.TryParse(subClaim, out var userId))
            return null;

        return userId;
    }

    private static bool ValidateOrgAccess(ClaimsPrincipal user, Guid orgId)
    {
        var orgClaim = user.FindFirst("org_id")?.Value;
        if (string.IsNullOrEmpty(orgClaim) || !Guid.TryParse(orgClaim, out var userOrgId))
            return false;

        return userOrgId == orgId;
    }

    private static FiscalDeviceResponse ToResponse(FiscalDeviceSnapshot snapshot, Guid siteId)
    {
        return new FiscalDeviceResponse(
            Id: snapshot.FiscalDeviceId,
            SiteId: siteId,
            DeviceType: snapshot.DeviceType.ToString(),
            SerialNumber: snapshot.SerialNumber,
            PublicKey: snapshot.PublicKey,
            CertificateExpiryDate: snapshot.CertificateExpiryDate,
            Status: snapshot.Status.ToString(),
            ApiEndpoint: snapshot.ApiEndpoint,
            LastSyncAt: snapshot.LastSyncAt,
            TransactionCounter: snapshot.TransactionCounter,
            SignatureCounter: snapshot.SignatureCounter,
            ClientId: snapshot.ClientId);
    }

    private static FiscalTransactionResponse ToResponse(FiscalTransactionSnapshot snapshot, Guid siteId)
    {
        return new FiscalTransactionResponse(
            Id: snapshot.FiscalTransactionId,
            FiscalDeviceId: snapshot.FiscalDeviceId,
            SiteId: siteId,
            TransactionNumber: snapshot.TransactionNumber,
            TransactionType: snapshot.TransactionType.ToString(),
            ProcessType: snapshot.ProcessType.ToString(),
            StartTime: snapshot.StartTime,
            EndTime: snapshot.EndTime,
            SourceType: snapshot.SourceType,
            SourceId: snapshot.SourceId,
            GrossAmount: snapshot.GrossAmount,
            NetAmounts: snapshot.NetAmounts,
            TaxAmounts: snapshot.TaxAmounts,
            PaymentTypes: snapshot.PaymentTypes,
            Signature: snapshot.Signature,
            SignatureCounter: snapshot.SignatureCounter,
            CertificateSerial: snapshot.CertificateSerial,
            QrCodeData: snapshot.QrCodeData,
            Status: snapshot.Status.ToString(),
            ErrorMessage: snapshot.ErrorMessage,
            RetryCount: snapshot.RetryCount,
            ExportedAt: snapshot.ExportedAt);
    }
}
