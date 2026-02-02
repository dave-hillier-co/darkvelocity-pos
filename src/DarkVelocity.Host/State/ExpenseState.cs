namespace DarkVelocity.Host.State;

/// <summary>
/// State for an expense record.
/// </summary>
[GenerateSerializer]
public sealed class ExpenseState
{
    [Id(0)] public Guid ExpenseId { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }

    /// <summary>Category of expense (Rent, Utilities, etc.)</summary>
    [Id(3)] public ExpenseCategory Category { get; set; }

    /// <summary>Custom category name if Category is Other</summary>
    [Id(4)] public string? CustomCategory { get; set; }

    /// <summary>Description of the expense</summary>
    [Id(5)] public string Description { get; set; } = string.Empty;

    /// <summary>Expense amount</summary>
    [Id(6)] public decimal Amount { get; set; }

    /// <summary>Currency code (USD, EUR, etc.)</summary>
    [Id(7)] public string Currency { get; set; } = "USD";

    /// <summary>Date the expense was incurred</summary>
    [Id(8)] public DateOnly ExpenseDate { get; set; }

    /// <summary>Optional vendor/payee ID</summary>
    [Id(9)] public Guid? VendorId { get; set; }

    /// <summary>Vendor/payee name</summary>
    [Id(10)] public string? VendorName { get; set; }

    /// <summary>Payment method used</summary>
    [Id(11)] public PaymentMethod? PaymentMethod { get; set; }

    /// <summary>Reference number (check #, transaction ID, etc.)</summary>
    [Id(12)] public string? ReferenceNumber { get; set; }

    /// <summary>URL to supporting document (receipt, invoice, etc.)</summary>
    [Id(13)] public string? DocumentUrl { get; set; }

    /// <summary>Original filename of uploaded document</summary>
    [Id(14)] public string? DocumentFilename { get; set; }

    /// <summary>Linked purchase document ID if this expense came from a confirmed document</summary>
    [Id(15)] public Guid? LinkedDocumentId { get; set; }

    /// <summary>Whether this expense is recurring</summary>
    [Id(16)] public bool IsRecurring { get; set; }

    /// <summary>Recurrence pattern if recurring</summary>
    [Id(17)] public RecurrencePattern? RecurrencePattern { get; set; }

    /// <summary>Tax amount if tracked separately</summary>
    [Id(18)] public decimal? TaxAmount { get; set; }

    /// <summary>Whether this expense is tax deductible</summary>
    [Id(19)] public bool IsTaxDeductible { get; set; }

    /// <summary>Notes or additional details</summary>
    [Id(20)] public string? Notes { get; set; }

    /// <summary>Tags for filtering/grouping</summary>
    [Id(21)] public List<string> Tags { get; set; } = [];

    /// <summary>Current status of the expense</summary>
    [Id(22)] public ExpenseStatus Status { get; set; } = ExpenseStatus.Pending;

    /// <summary>Who approved this expense (if applicable)</summary>
    [Id(23)] public Guid? ApprovedBy { get; set; }

    /// <summary>When the expense was approved</summary>
    [Id(24)] public DateTime? ApprovedAt { get; set; }

    // Audit fields
    [Id(25)] public DateTime CreatedAt { get; set; }
    [Id(26)] public Guid CreatedBy { get; set; }
    [Id(27)] public DateTime? UpdatedAt { get; set; }
    [Id(28)] public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// Categories for expenses.
/// </summary>
public enum ExpenseCategory
{
    /// <summary>Rent and lease payments</summary>
    Rent,
    /// <summary>Utilities (electric, gas, water)</summary>
    Utilities,
    /// <summary>Insurance premiums</summary>
    Insurance,
    /// <summary>Equipment purchases and repairs</summary>
    Equipment,
    /// <summary>Maintenance and repairs</summary>
    Maintenance,
    /// <summary>Marketing and advertising</summary>
    Marketing,
    /// <summary>Non-food supplies (cleaning, paper goods)</summary>
    Supplies,
    /// <summary>Professional services (accounting, legal)</summary>
    Professional,
    /// <summary>Bank fees</summary>
    BankFees,
    /// <summary>Credit card processing fees</summary>
    CreditCardFees,
    /// <summary>Licenses and permits</summary>
    Licenses,
    /// <summary>Payroll expenses</summary>
    Payroll,
    /// <summary>Training and education</summary>
    Training,
    /// <summary>Travel and transportation</summary>
    Travel,
    /// <summary>Technology and software</summary>
    Technology,
    /// <summary>Other/miscellaneous</summary>
    Other
}

/// <summary>
/// Payment methods for expenses.
/// </summary>
public enum PaymentMethod
{
    Cash,
    Check,
    CreditCard,
    DebitCard,
    BankTransfer,
    ACH,
    Wire,
    PettyCash,
    Other
}

/// <summary>
/// Status of an expense record.
/// </summary>
public enum ExpenseStatus
{
    /// <summary>Expense recorded, awaiting review</summary>
    Pending,
    /// <summary>Expense approved</summary>
    Approved,
    /// <summary>Expense paid/settled</summary>
    Paid,
    /// <summary>Expense rejected</summary>
    Rejected,
    /// <summary>Expense voided/cancelled</summary>
    Voided
}

/// <summary>
/// Recurrence pattern for recurring expenses.
/// </summary>
[GenerateSerializer]
public sealed record RecurrencePattern
{
    [Id(0)] public RecurrenceFrequency Frequency { get; init; }
    [Id(1)] public int Interval { get; init; } = 1; // Every N periods
    [Id(2)] public int? DayOfMonth { get; init; } // For monthly
    [Id(3)] public DayOfWeek? DayOfWeek { get; init; } // For weekly
    [Id(4)] public DateOnly? EndDate { get; init; } // When to stop recurring
    [Id(5)] public int? MaxOccurrences { get; init; } // Max number of occurrences
}

/// <summary>
/// Frequency of recurrence.
/// </summary>
public enum RecurrenceFrequency
{
    Weekly,
    BiWeekly,
    Monthly,
    Quarterly,
    Annually
}
