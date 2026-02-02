using DarkVelocity.Host.Payments;
using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

public class PaymentMethodGrain : Grain, IPaymentMethodGrain
{
    private readonly IPersistentState<PaymentMethodState> _state;
    private readonly ICardValidationService _cardValidation;

    public PaymentMethodGrain(
        [PersistentState("paymentMethod", "OrleansStorage")]
        IPersistentState<PaymentMethodState> state,
        ICardValidationService cardValidation)
    {
        _state = state;
        _cardValidation = cardValidation;
    }

    public async Task<PaymentMethodSnapshot> CreateAsync(CreatePaymentMethodCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("PaymentMethod already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var accountId = Guid.Parse(parts[0]);
        var paymentMethodId = Guid.Parse(parts[2]); // accountId:pm:paymentMethodId

        _state.State = new PaymentMethodState
        {
            Id = paymentMethodId,
            AccountId = accountId,
            Type = command.Type,
            BillingDetails = command.BillingDetails,
            Metadata = command.Metadata,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        // Process card details
        if (command.Type == PaymentMethodType.Card && command.Card != null)
        {
            ProcessCardDetails(command.Card);
        }

        // Process bank account details
        if (command.Type == PaymentMethodType.BankAccount && command.BankAccount != null)
        {
            ProcessBankAccountDetails(command.BankAccount);
        }

        await _state.WriteStateAsync();

        return GetSnapshot();
    }

    public Task<PaymentMethodSnapshot> GetSnapshotAsync()
    {
        EnsureExists();
        return Task.FromResult(GetSnapshot());
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.Id != Guid.Empty);

    public Task<string> GetProcessorTokenAsync()
    {
        EnsureExists();

        if (string.IsNullOrEmpty(_state.State.ProcessorToken))
        {
            // Generate a token for the mock processor
            // In production, this would be a real processor token
            _state.State.ProcessorToken = $"pm_card_{_state.State.CardLast4}";
            _state.State.ProcessorName = "mock";
        }

        return Task.FromResult(_state.State.ProcessorToken);
    }

    public async Task<PaymentMethodSnapshot> AttachToCustomerAsync(string customerId)
    {
        EnsureExists();

        if (!string.IsNullOrEmpty(_state.State.CustomerId))
            throw new InvalidOperationException("PaymentMethod is already attached to a customer");

        _state.State.CustomerId = customerId;
        _state.State.Version++;

        await _state.WriteStateAsync();

        return GetSnapshot();
    }

    public async Task<PaymentMethodSnapshot> DetachFromCustomerAsync()
    {
        EnsureExists();

        if (string.IsNullOrEmpty(_state.State.CustomerId))
            throw new InvalidOperationException("PaymentMethod is not attached to any customer");

        _state.State.CustomerId = null;
        _state.State.Version++;

        await _state.WriteStateAsync();

        return GetSnapshot();
    }

    public async Task<PaymentMethodSnapshot> UpdateAsync(
        BillingDetails? billingDetails = null,
        int? expMonth = null,
        int? expYear = null,
        Dictionary<string, string>? metadata = null)
    {
        EnsureExists();

        if (billingDetails != null)
        {
            _state.State.BillingDetails = billingDetails;
        }

        if (expMonth.HasValue && _state.State.Type == PaymentMethodType.Card)
        {
            if (expMonth < 1 || expMonth > 12)
                throw new ArgumentException("Invalid expiry month");
            _state.State.CardExpMonth = expMonth;
        }

        if (expYear.HasValue && _state.State.Type == PaymentMethodType.Card)
        {
            var fullYear = expYear < 100 ? 2000 + expYear : expYear;
            _state.State.CardExpYear = fullYear;
        }

        if (metadata != null)
        {
            _state.State.Metadata = metadata;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();

        return GetSnapshot();
    }

    private void ProcessCardDetails(CardDetails card)
    {
        // Validate card number
        if (!_cardValidation.ValidateCardNumber(card.Number))
            throw new ArgumentException("Invalid card number");

        // Validate expiry
        if (!_cardValidation.ValidateExpiry(card.ExpMonth, card.ExpYear))
            throw new ArgumentException("Card is expired");

        // Validate CVC if provided
        var brand = _cardValidation.GetCardBrand(card.Number);
        if (!string.IsNullOrEmpty(card.Cvc) && !_cardValidation.ValidateCvc(card.Cvc, brand))
            throw new ArgumentException("Invalid CVC");

        // Store card details (in production, only tokenized reference would be stored)
        _state.State.CardBrand = brand;
        _state.State.CardLast4 = _cardValidation.GetLast4(card.Number);
        _state.State.CardExpMonth = card.ExpMonth;
        _state.State.CardExpYear = card.ExpYear < 100 ? 2000 + card.ExpYear : card.ExpYear;
        _state.State.CardFingerprint = _cardValidation.GenerateFingerprint(card.Number);
        _state.State.CardholderName = card.CardholderName;
        _state.State.CardFunding = _cardValidation.DetectFundingType(card.Number);
        _state.State.CardCountry = _cardValidation.DetectCardCountry(card.Number);

        // Generate processor token
        _state.State.ProcessorToken = GenerateProcessorToken(card.Number);
        _state.State.ProcessorName = "mock";
    }

    private void ProcessBankAccountDetails(BankAccountDetails bankAccount)
    {
        _state.State.BankAccountCountry = bankAccount.Country;
        _state.State.BankAccountCurrency = bankAccount.Currency;
        _state.State.BankAccountHolderName = bankAccount.AccountHolderName;
        _state.State.BankAccountHolderType = bankAccount.AccountHolderType;
        _state.State.BankRoutingNumber = bankAccount.RoutingNumber;
        _state.State.BankAccountStatus = "new";

        // Extract last 4 from account number or IBAN
        if (!string.IsNullOrEmpty(bankAccount.AccountNumber))
        {
            _state.State.BankAccountLast4 = bankAccount.AccountNumber.Length >= 4
                ? bankAccount.AccountNumber[^4..]
                : bankAccount.AccountNumber;
        }
        else if (!string.IsNullOrEmpty(bankAccount.Iban))
        {
            _state.State.BankAccountLast4 = bankAccount.Iban.Length >= 4
                ? bankAccount.Iban[^4..]
                : bankAccount.Iban;
        }

        // Generate processor token
        _state.State.ProcessorToken = $"ba_{Guid.NewGuid():N}";
        _state.State.ProcessorName = "mock";
    }

    private static string GenerateProcessorToken(string cardNumber)
    {
        // In production, this would be a token from the actual processor
        // For the mock processor, we encode enough info to simulate behavior
        var last4 = cardNumber.Length >= 4 ? cardNumber[^4..] : cardNumber;
        return $"pm_card_{last4}";
    }

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("PaymentMethod does not exist");
    }

    private PaymentMethodSnapshot GetSnapshot()
    {
        CardSnapshot? cardSnapshot = null;
        BankAccountSnapshot? bankAccountSnapshot = null;

        if (_state.State.Type == PaymentMethodType.Card && !string.IsNullOrEmpty(_state.State.CardLast4))
        {
            cardSnapshot = new CardSnapshot(
                Brand: _state.State.CardBrand ?? "unknown",
                Last4: _state.State.CardLast4,
                ExpMonth: _state.State.CardExpMonth ?? 0,
                ExpYear: _state.State.CardExpYear ?? 0,
                Fingerprint: _state.State.CardFingerprint ?? "",
                CardholderName: _state.State.CardholderName,
                Funding: _state.State.CardFunding ?? "credit",
                Country: _state.State.CardCountry);
        }

        if (_state.State.Type == PaymentMethodType.BankAccount && !string.IsNullOrEmpty(_state.State.BankAccountLast4))
        {
            bankAccountSnapshot = new BankAccountSnapshot(
                Country: _state.State.BankAccountCountry ?? "",
                Currency: _state.State.BankAccountCurrency ?? "",
                Last4: _state.State.BankAccountLast4,
                AccountHolderName: _state.State.BankAccountHolderName ?? "",
                AccountHolderType: _state.State.BankAccountHolderType ?? "",
                BankName: _state.State.BankName,
                RoutingNumber: _state.State.BankRoutingNumber,
                Status: _state.State.BankAccountStatus ?? "new");
        }

        return new PaymentMethodSnapshot(
            Id: _state.State.Id,
            AccountId: _state.State.AccountId,
            Type: _state.State.Type,
            CustomerId: _state.State.CustomerId,
            Card: cardSnapshot,
            BankAccount: bankAccountSnapshot,
            BillingDetails: _state.State.BillingDetails,
            Metadata: _state.State.Metadata,
            CreatedAt: _state.State.CreatedAt,
            Livemode: false);
    }
}
