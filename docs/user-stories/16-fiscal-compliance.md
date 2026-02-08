# Fiscal Compliance User Stories

Stories extracted from unit test specifications covering transaction sequencing, signature chain validation, tamper detection, journal immutability, device registration and lifecycle, TSE self-tests, Z-reports and daily close, audit export formats (DSFinV-K), multi-country fiscal configuration, country-specific formats (Germany, France, Poland, Austria, Italy), fiscal job scheduling, fiscal feature support, and high-volume journal operations.

## Transaction Sequencing

### Sequential Transaction Numbers

**As a** compliance officer,
**I want to** ensure that TSE transaction numbers are strictly sequential with no gaps,
**So that** the system meets KassenSichV, NF525, and RKSV requirements for unbroken transaction numbering.

- Given: A TSE device initialized
- When: Multiple transactions are processed
- Then: Transaction numbers must be sequential with no gaps

### Sequential Signature Counters

**As a** compliance officer,
**I want to** ensure that TSE signature counters are strictly sequential with no gaps,
**So that** every signed transaction can be accounted for in an audit without missing entries.

- Given: A TSE device initialized
- When: Multiple transactions are signed
- Then: Signature counters must be sequential with no gaps

### Monotonically Increasing Transaction Counter on Fiscal Device

**As a** compliance officer,
**I want to** ensure that a fiscal device's transaction counter is strictly monotonically increasing,
**So that** no two transactions share the same counter value and no counter values are skipped.

- Given: A fiscal device registered
- When: Getting multiple transaction counters
- Then: Counters must be strictly increasing

### Monotonically Increasing Signature Counter on Fiscal Device

**As a** compliance officer,
**I want to** ensure that a fiscal device's signature counter is strictly monotonically increasing,
**So that** the integrity of the signature chain is maintained across all signed transactions.

- Given: A fiscal device registered
- When: Getting multiple signature counters
- Then: Counters must be strictly increasing

### Counter Persistence Across Reactivation

**As a** system administrator,
**I want to** ensure that transaction and signature counters persist across grain reactivation,
**So that** system restarts or grain deactivation cycles do not reset counters and create compliance gaps.

- Given: A TSE with some transactions
- When: Getting a fresh reference to the same grain (simulating reactivation) and starting another transaction
- Then: Counters must continue from where they left off

## Signature Chain Validation

### Unique Signatures

**As a** compliance officer,
**I want to** ensure that every signature produced by the TSE is unique,
**So that** replay attacks are not possible and each transaction is individually verifiable.

- Given: A TSE device
- When: Generating multiple signatures
- Then: All signatures must be unique (no replay attacks possible)

### Certificate Serial Presence

**As an** auditor,
**I want to** ensure that every signed transaction includes the TSE certificate serial number,
**So that** each signature can be traced back to the specific TSE device that produced it.

- Given: A TSE device
- When: Completing a transaction
- Then: Certificate serial must be present

### Consistent Certificate Across Transactions

**As an** auditor,
**I want to** ensure that the certificate serial is consistent across all transactions from the same TSE,
**So that** there is no evidence of unauthorized device swapping during a business period.

- Given: A TSE device
- When: Completing multiple transactions
- Then: Certificate serial must be consistent (same TSE = same certificate)

### Compliant Signature Algorithm

**As a** compliance officer,
**I want to** ensure that the TSE uses a compliant signature algorithm (HMAC-SHA256 or ECDSA),
**So that** signed data meets the cryptographic requirements of KassenSichV.

- Given: A TSE device
- When: Completing a transaction
- Then: Signature algorithm must be compliant (HMAC-SHA256 or ECDSA)

### QR Code Required Fields

**As a** compliance officer,
**I want to** ensure that the QR code data contains all required KassenSichV fields,
**So that** printed receipts include machine-readable fiscal data for tax authority verification.

- Given: A TSE device
- When: Completing a transaction
- Then: QR code must contain required KassenSichV fields

### Transaction Time Ordering

**As an** auditor,
**I want to** ensure that the start time of a transaction always precedes the end time,
**So that** the temporal integrity of transaction records is maintained.

- Given: A TSE device
- When: Completing a transaction
- Then: Start time must be before end time

### Consistent Public Key

**As a** compliance officer,
**I want to** ensure that the TSE public key remains consistent across snapshots and transactions,
**So that** signature verification can be performed against a stable, known key.

- Given: A TSE device
- When: Getting snapshots and completing transactions
- Then: Public key must be consistent

## Tamper Detection

### Preventing Re-signing of Transactions

**As a** compliance officer,
**I want to** prevent a signed transaction from being re-signed with a different signature,
**So that** fiscal records cannot be tampered with after the fact.

- Given: A signed transaction
- When: Attempting to resign
- Then: Should reject the second signature

### Immutable Signed Status

**As an** auditor,
**I want to** ensure that once a transaction is signed its status cannot be reverted,
**So that** the audit trail reflects the true finality of fiscal transactions.

- Given: A signed transaction
- When: Checking the status
- Then: Status must be Signed

### Counter Reset Prevention

**As a** compliance officer,
**I want to** ensure that fiscal device counters can never be reset,
**So that** there is no way to create gaps or overlaps in the transaction numbering sequence.

- Given: A device with incremented counters
- When: Getting more counters
- Then: Counters must only increase, never reset

## Journal Immutability and Audit Trail

### Append-Only Journal Entries

**As an** auditor,
**I want to** ensure that journal entries are append-only and cannot be deleted,
**So that** the fiscal audit trail is complete and tamper-evident.

- Given: A journal with entries
- When: Adding more entries (the only allowed operation)
- Then: Count should only increase, never decrease

### Unique Entry Identifiers

**As an** auditor,
**I want to** ensure that all journal entry IDs are unique,
**So that** every fiscal event can be individually referenced during an audit.

- Given: A journal with multiple entries
- When: Retrieving all entries
- Then: All entry IDs must be unique

### Chronological Timestamp Ordering

**As an** auditor,
**I want to** ensure that journal timestamps are in chronological order,
**So that** the sequence of fiscal events can be reliably reconstructed.

- Given: A journal with entries logged in sequence
- When: Retrieving all entries
- Then: Timestamps must be in chronological order

### All Event Types Preserved

**As a** compliance officer,
**I want to** ensure that the journal preserves all fiscal event types,
**So that** no category of fiscal activity is excluded from the audit record.

- Given: A journal with various event types (compliance requires all types preserved)
- When: Retrieving entries
- Then: All event types must be preserved

### Permanent Error Recording

**As a** compliance officer,
**I want to** ensure that error events are permanently recorded in the fiscal journal,
**So that** device failures and signing errors are available for regulatory review.

- Given: A journal with error events
- When: Retrieving errors
- Then: All errors must be permanently recorded

## High Volume and Filtering

### High-Volume Journal Handling

**As a** system administrator,
**I want to** ensure that the fiscal journal can handle at least 1000 entries in a single day,
**So that** high-volume venues do not lose fiscal data during peak periods.

- Given: A fiscal journal
- When: Logging 1000 entries (high volume day simulation)
- Then: All entries should be stored and retrievable

### Device-Level Filtering

**As an** auditor,
**I want to** filter journal entries by device,
**So that** I can isolate and review the fiscal activity of a specific terminal.

- Given: A journal with entries from multiple devices
- When: Filtering by device
- Then: Each device should have approximately 1/3 of entries

## Device Registration and Lifecycle

### Unique Serial Numbers in Registry

**As a** compliance officer,
**I want to** ensure that each fiscal device has a unique serial number in the registry,
**So that** tax authorities can uniquely identify every registered device.

- Given: A device registry
- When: Registering devices with unique serial numbers
- Then: Each serial should map to correct device

### Serial Number Required for Registration

**As a** compliance officer,
**I want to** require a serial number for every fiscal device registration,
**So that** no device operates without proper identification.

- Given: Device registration attempt
- When: Registration with serial number should succeed
- Then: Serial number is present and not empty

### Device Deregistration Tracking

**As a** compliance officer,
**I want to** track when a fiscal device is deregistered,
**So that** the full device lifecycle is documented for regulatory purposes.

- Given: A registered device
- When: Unregistering the device
- Then: Device should be removed from registry

### Device Activate and Deactivate Lifecycle

**As a** manager,
**I want to** activate and deactivate fiscal devices with recorded reasons,
**So that** device status changes are auditable and traceable to specific operators.

- Given: A registered device
- When: Deactivating with reason
- Then: Device should be inactive
- When: Reactivating
- Then: Device should be active

### Self-Test Passes for Active Device

**As a** system administrator,
**I want to** verify that self-tests pass for active devices with valid certificates,
**So that** I can confirm the device is operational before processing transactions.

- Given: An active device with valid certificate
- When: Performing self-test
- Then: Self-test should pass

### Self-Test Fails for Expired Certificate

**As a** compliance officer,
**I want to** ensure that self-tests fail for devices with expired certificates,
**So that** expired devices are detected and taken out of service before producing invalid signatures.

- Given: A device with expired certificate
- When: Performing self-test
- Then: Self-test should fail

## TSE Self-Test Compliance

### Self-Test Updates Timestamp

**As a** compliance officer,
**I want to** ensure that performing a TSE self-test updates the last self-test timestamp,
**So that** there is a record of when the most recent self-test was executed.

- Given: An initialized TSE
- When: Performing self-test
- Then: Timestamp should be updated

### Self-Test Records Result in Snapshot

**As an** auditor,
**I want to** ensure that self-test results are recorded in the TSE snapshot,
**So that** the current health status of the TSE is always available for inspection.

- Given: An initialized TSE
- When: Performing self-test
- Then: Result should be recorded in snapshot

### Self-Test on Uninitialized TSE

**As a** system administrator,
**I want to** ensure that self-tests cannot be performed on uninitialized TSE devices,
**So that** meaningless test results are not recorded for devices that have not been properly set up.

- Given: An uninitialized TSE
- When: Performing self-test
- Then: Should throw an error indicating TSE not initialized

## Z-Reports and Daily Close

### Sequential Z-Report Numbers

**As a** compliance officer,
**I want to** ensure that Z-report numbers are sequential,
**So that** daily closing reports form an unbroken chain as required by fiscal regulations.

- Given: A site with Z-Report capability
- When: No reports exist
- Then: Latest should be null

### Daily Close Without Configuration

**As a** manager,
**I want to** receive a clear failure when attempting a daily close on an unconfigured site,
**So that** I know fiscal configuration is required before generating compliance reports.

- Given: A site without fiscal configuration
- When: Attempting daily close
- Then: Should return failure due to no configuration

### Empty Date Range Query

**As an** auditor,
**I want to** query Z-reports by date range and receive an empty result when none exist,
**So that** I can safely query any time period without encountering errors.

- Given: A fresh site
- When: Querying reports for a date range
- Then: Should return empty list

### Non-Existent Report Lookup

**As an** auditor,
**I want to** look up a Z-report by number and receive null when it does not exist,
**So that** I can verify the presence or absence of specific daily closing reports.

- Given: A site
- When: Querying non-existent report
- Then: Should return null

### Latest Report When No Reports Exist

**As a** manager,
**I want to** check the latest Z-report and receive null when no reports have been generated,
**So that** the system clearly indicates when a site has no fiscal reporting history.

- Given: A Z-report grain for a site with no prior reports generated
- When: Requesting the latest Z-report
- Then: Null is returned

### Reports Query for Empty Site

**As an** auditor,
**I want to** query Z-reports for the past month on a new site and get an empty list,
**So that** date range queries work correctly even before any reports are generated.

- Given: A Z-report grain for a site with no prior reports generated
- When: Querying reports for the past month
- Then: An empty list is returned

### Report Lookup by Number for Missing Report

**As an** auditor,
**I want to** request a specific report by number and get null when it does not exist,
**So that** the system handles missing report lookups gracefully.

- Given: A Z-report grain for a site with no prior reports generated
- When: Requesting a specific report by number 999 that does not exist
- Then: Null is returned

## Audit Export Formats (DSFinV-K)

### Export Creation Lifecycle

**As a** compliance officer,
**I want to** create a DSFinV-K export with a date range and description,
**So that** I can generate tax audit files as required by German fiscal regulations.

- Given: Export request
- When: Creating an export
- Then: Export should be created with correct metadata

### Export Status Transitions

**As a** compliance officer,
**I want to** track DSFinV-K export status through pending, processing, and completed states,
**So that** I can monitor the progress of audit file generation.

- Given: A pending export
- When: Transitioning through statuses
- Then: Status transitions to Processing and then to Completed with transaction count and file path

### Export Failure Tracking

**As a** compliance officer,
**I want to** record failure details when a DSFinV-K export fails,
**So that** I can diagnose and retry failed audit exports.

- Given: A pending export
- When: Export fails
- Then: Status should be failed with error message

### Export Registry Tracking

**As an** auditor,
**I want to** see all DSFinV-K exports tracked in reverse chronological order,
**So that** I can review the history of audit file generation for a site.

- Given: An export registry
- When: Registering multiple exports
- Then: All exports should be tracked in reverse chronological order

### Audit Export Requires Configuration

**As a** compliance officer,
**I want to** prevent audit exports from being generated on unconfigured sites,
**So that** exports are only created when proper fiscal settings are in place.

- Given: A site without fiscal configuration
- When: Attempting audit export
- Then: Should throw an error indicating not configured

## Multi-Country Fiscal Configuration

### Germany Configuration

**As a** manager,
**I want to** configure a site for German fiscal compliance with a Fiskaly Cloud TSE device,
**So that** the site operates under KassenSichV regulations.

- Given: An unconfigured multi-country fiscal grain for a site
- When: Configuring it for Germany with a Fiskaly Cloud TSE device and API credentials
- Then: The compliance standard is set to "KassenSichV" and the TSE type is FiskalyCloud

### France Configuration

**As a** manager,
**I want to** configure a site for French fiscal compliance with NF525 certification,
**So that** the site operates under NF 525 regulations.

- Given: An unconfigured multi-country fiscal grain for a site
- When: Configuring it for France with NF525 certification number and SIREN
- Then: The compliance standard is set to "NF 525"

### Poland Configuration

**As a** manager,
**I want to** configure a site for Polish fiscal compliance with NIP and KSeF,
**So that** the site operates under JPK/KSeF regulations.

- Given: An unconfigured multi-country fiscal grain for a site
- When: Configuring it for Poland with NIP and KSeF enabled
- Then: The compliance standard is set to "JPK/KSeF"

### Austria Configuration Snapshot

**As a** manager,
**I want to** retrieve the fiscal configuration snapshot for an Austrian site,
**So that** I can verify the correct org, site, country, and RKSV compliance standard are applied.

- Given: A multi-country fiscal grain configured for Austria with a Swissbit Cloud TSE
- When: Retrieving the fiscal configuration snapshot
- Then: The snapshot contains the correct org ID, site ID, country, and RKSV compliance standard

### Validation of Unconfigured Site

**As a** compliance officer,
**I want to** validate a site's fiscal configuration and get clear errors when it is not configured,
**So that** misconfigured sites are identified before they begin processing transactions.

- Given: A multi-country fiscal grain that has never been configured
- When: Validating the fiscal configuration
- Then: Validation fails with an error indicating the site is "not configured"

### Empty Features for Unconfigured Site

**As a** system administrator,
**I want to** query supported fiscal features and get an empty set for unconfigured sites,
**So that** the system does not advertise capabilities that are not yet available.

- Given: A multi-country fiscal grain that has never been configured
- When: Querying the supported fiscal features
- Then: An empty feature set is returned

### Transaction Recording Requires Configuration

**As a** compliance officer,
**I want to** prevent fiscal transaction recording on unconfigured sites,
**So that** transactions are not processed without proper fiscal compliance settings.

- Given: A multi-country fiscal grain that has never been configured
- When: Attempting to record a 119.00 EUR receipt transaction
- Then: The operation fails with error code "NOT_CONFIGURED"

### Disabling Fiscal Integration

**As a** manager,
**I want to** disable fiscal integration for a site,
**So that** fiscal signing can be turned off when a site is closed or compliance is handled externally.

- Given: A multi-country fiscal grain configured and enabled for Italy
- When: Reconfiguring with Enabled set to false
- Then: The fiscal integration is disabled

## Country-Specific Formats

### Germany

#### Compliance Standard Mapping

**As a** compliance officer,
**I want to** verify that Germany maps to the KassenSichV compliance standard,
**So that** the correct regulatory framework is applied to German sites.

- Given: A fiscal country (Germany, Austria, Italy, France, or Poland)
- When: Resolving the compliance standard for that country
- Then: The correct standard is returned (KassenSichV, RKSV, RT, NF 525, or JPK/KSeF respectively)

#### Supported TSE Device Types

**As a** system administrator,
**I want to** confirm that all TSE device types (SwissbitCloud, SwissbitUsb, FiskalyCloud, Epson, Diebold) are supported,
**So that** German venues can use any approved TSE hardware or cloud provider.

- Given: The set of TSE device types defined in the system
- When: Checking each device type against the supported list
- Then: All device types are documented as supported

#### Required Process Types

**As a** compliance officer,
**I want to** verify that all KassenSichV-required process types are defined,
**So that** every category of fiscal transaction (Kassenbeleg, AVTransfer, AVBestellung, AVSonstiger) can be properly classified.

- Given: The KassenSichV-required process types
- When: Checking if each process type is defined in the enum
- Then: All required process types exist

#### Hardware and Cloud TSE Feature Support

**As a** system administrator,
**I want to** verify that the German fiscal adapter supports both hardware and cloud TSE,
**So that** venues can choose the deployment model that fits their infrastructure.

- Given: The expected feature set for the German KassenSichV fiscal adapter
- When: Checking for hardware TSE and cloud TSE support
- Then: Both HardwareTse and CloudTse features are present

### France

#### NF 525 Cumulative Totals Tracking

**As a** compliance officer,
**I want to** track French cumulative totals including grand totals, transaction counts, and VAT breakdowns,
**So that** the system meets NF 525 requirements for perpetual running totals.

- Given: French cumulative totals with defined grand total and VAT rate breakdowns
- When: Checking the totals structure
- Then: Grand total, transaction count, void count, and VAT rate breakdowns are all tracked

#### Adding a Transaction to Cumulative Totals

**As a** compliance officer,
**I want to** ensure that adding a transaction increments the French cumulative grand total and transaction count,
**So that** the perpetual running total always reflects the latest sales activity.

- Given: French cumulative totals with a grand total of 1000.00 and 10 transactions
- When: Adding a 119.00 EUR transaction to the perpetual grand total
- Then: The grand total increases to 1119.00 and the transaction count becomes 11

#### Void Transaction Tracking

**As a** compliance officer,
**I want to** ensure that void transactions are tracked separately in the French cumulative totals,
**So that** voids are auditable and the grand total is adjusted accordingly.

- Given: French cumulative totals with a grand total of 1000.00 and no voids
- When: Processing a 50.00 EUR void transaction
- Then: The grand total decreases to 950.00, void count is 1, and void total is 50.00

#### VAT Rate Aggregation

**As a** compliance officer,
**I want to** ensure that French cumulative totals aggregate correctly by VAT rate,
**So that** tax breakdowns are accurate for each applicable rate.

- Given: French cumulative totals with existing VAT breakdowns at 20%, 10%, and 5.5%
- When: Adding amounts to existing rates and introducing a new 2.1% rate
- Then: Existing rate totals are updated and the new rate is tracked, totaling 4 VAT categories

#### Monotonic Sequence Numbers

**As a** compliance officer,
**I want to** ensure that French cumulative total sequence numbers increment monotonically,
**So that** transaction ordering is preserved for NF 525 compliance.

- Given: French cumulative totals with a sequence number of 100
- When: Processing three consecutive transactions
- Then: Sequence numbers increment monotonically to 101, 102, and 103

#### French Adapter Feature Support

**As a** compliance officer,
**I want to** verify that the French fiscal adapter supports cumulative totals and electronic journals,
**So that** NF 525 requirements for running totals and event logging are met.

- Given: The expected feature set for the French NF 525 fiscal adapter
- When: Checking for cumulative totals and electronic journal support
- Then: Both CumulativeTotals and ElectronicJournal features are present

### Poland

#### NIP Validation

**As a** compliance officer,
**I want to** validate Polish NIP (tax identification numbers) against the checksum algorithm,
**So that** only valid tax IDs are accepted in the system.

- Given: Various Polish NIP (tax identification number) inputs including valid, invalid checksum, wrong length, and non-numeric
- When: Validating each NIP against the Polish checksum algorithm
- Then: Only correctly formatted 10-digit NIPs with valid checksums pass validation

#### VAT Rate Mapping

**As a** compliance officer,
**I want to** verify that Polish VAT rates map to the correct decimal values,
**So that** tax calculations use the legally mandated rates.

- Given: The Polish VAT rate enum values (23%, 8%, 5%, 0%, Exempt)
- When: Mapping each enum value to its decimal rate
- Then: Each rate maps to the correct decimal value

#### VAT Entry Document Formatting

**As a** compliance officer,
**I want to** ensure that Polish VAT entries correctly format document numbers and compute gross amounts,
**So that** JPK submissions contain properly structured invoice data.

- Given: A Polish VAT entry for invoice "FV/123/2024" with 100.00 net and 23.00 VAT at 23% rate
- When: Creating the VAT entry record
- Then: The document number starts with "FV/" and ends with "/2024", and gross equals net plus VAT

#### KSeF Invoice Submission Tracking

**As a** compliance officer,
**I want to** track KSeF invoice submission status through pending and accepted states,
**So that** the system records when invoices are submitted to and accepted by the Polish tax authority.

- Given: A pending KSeF invoice record with no KSeF number assigned
- When: Simulating acceptance by updating the status to Accepted with a KSeF reference number
- Then: The accepted record has a KSeF number, submission timestamp, and acceptance timestamp

#### Polish Adapter Feature Support

**As a** compliance officer,
**I want to** verify that the Polish fiscal adapter supports VAT register export and invoice verification,
**So that** JPK/KSeF requirements for tax reporting and invoice validation are met.

- Given: The expected feature set for the Polish JPK/KSeF fiscal adapter
- When: Checking for VAT register export and invoice verification support
- Then: Both VatRegisterExport and InvoiceVerification features are present

## Multi-Country Routing

### All Documented Countries Supported

**As a** system administrator,
**I want to** verify that the fiscal adapter factory supports all documented countries,
**So that** every country in the system has a corresponding fiscal compliance implementation.

- Given: The set of documented supported countries (Germany, Austria, Italy, France, Poland)
- When: Checking all FiscalCountry enum values against the supported set
- Then: Every enum value is in the supported countries list

### External TSE Types

**As a** system administrator,
**I want to** verify that all expected external TSE types are defined,
**So that** the system supports the full range of TSE hardware and cloud provider integrations.

- Given: The expected set of external TSE types (None, SwissbitCloud, SwissbitUsb, FiskalyCloud, Epson, Diebold, Custom)
- When: Checking the ExternalTseType enum values
- Then: All expected TSE hardware and cloud provider types are present

## Fiscal Job Scheduling

### Configure Site Jobs

**As a** manager,
**I want to** configure automated fiscal jobs for a site including daily close time, archive time, and certificate monitoring,
**So that** fiscal compliance tasks run on schedule without manual intervention.

- Given: A fiscal job scheduler grain for an organization
- When: Configuring a site with daily close at 23:30, archive at 03:00, and certificate monitoring with 30-day warning
- Then: The configuration is stored with the correct times, monitoring settings, and timezone

### Remove Site Jobs

**As a** manager,
**I want to** remove a site's fiscal job configuration from the scheduler,
**So that** automated tasks stop running when a site is closed or no longer requires fiscal compliance.

- Given: A fiscal job scheduler with an existing site job configuration
- When: Removing the site's job configuration
- Then: The site is no longer listed in the scheduler's configurations

### Empty Job History

**As a** manager,
**I want to** query the job execution history and receive an empty list when no jobs have run,
**So that** the system clearly indicates when fiscal automation has not yet been executed.

- Given: A new fiscal job scheduler with no jobs executed
- When: Querying the job execution history
- Then: An empty history is returned

### Certificate Expiry Monitoring

**As a** compliance officer,
**I want to** check for TSE certificate expiry warnings and receive none when no certificates are registered,
**So that** the monitoring system does not produce false alerts on fresh installations.

- Given: A fiscal job scheduler with no TSE certificates registered
- When: Checking for certificate expiry warnings
- Then: No warnings are returned
