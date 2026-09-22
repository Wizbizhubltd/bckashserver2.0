using BCKash.Domain.Organization;

namespace BCKash.Api.Contracts;

// ---- Offices ----

public record OfficeResponse(
    int Id, string? Name, int? ParentId, string? ExternalId, DateOnly? OpeningDate,
    string? Address, string? Phone, string? Email, string? Notes, int? ManagerId,
    bool Active, bool DefaultOffice);

public record SaveOfficeRequest(
    string? Name, int? ParentId, string? ExternalId, DateOnly? OpeningDate,
    string? Address, string? Phone, string? Email, string? Notes, int? ManagerId,
    bool DefaultOffice);

public record OfficeInUseResponse(int ActiveClientCount, int OpenLoanCount);

// ---- Currencies ----

public record CurrencyResponse(int Id, string? Name, string? Code, string? Symbol, string? Decimals, decimal? Xrate, string? InternationalCode, bool Active);

public record SaveCurrencyRequest(string? Name, string? Code, string? Symbol, string? Decimals, decimal? Xrate, string? InternationalCode, bool Active);

// ---- Countries ----

public record CountryResponse(int Id, string Sortname, string Name);

public record SaveCountryRequest(string Sortname, string Name);

// ---- Funds ----

public record FundResponse(int Id, string? Name);

public record SaveFundRequest(string? Name);

// ---- Payment Types & Details ----

public record PaymentTypeResponse(int Id, string? Name, string? Notes, bool IsCash);

public record SavePaymentTypeRequest(string? Name, string? Notes, bool IsCash);

public record PaymentDetailResponse(
    int Id, int? PaymentTypeId, string? AccountNumber, string? ChequeNumber,
    string? RoutingCode, string? ReceiptNumber, string? Bank, string? Notes);

public record SavePaymentDetailRequest(
    int? PaymentTypeId, string? AccountNumber, string? ChequeNumber,
    string? RoutingCode, string? ReceiptNumber, string? Bank, string? Notes);

// ---- Charges ----

public record ChargeResponse(
    int Id, string? Name, int? CurrencyId, ChargeProduct Product, ChargeType ChargeType, ChargeOption ChargeOption,
    int ChargeFrequency, ChargeFrequencyType ChargeFrequencyType, int ChargeFrequencyAmount,
    decimal? Amount, decimal? MinimumAmount, decimal? MaximumAmount, ChargePaymentMode ChargePaymentMode,
    bool Active, bool Penalty, bool Override, int? GlAccountIncomeId);

public record SaveChargeRequest(
    string? Name, int? CurrencyId, ChargeProduct Product, ChargeType ChargeType, ChargeOption ChargeOption,
    int ChargeFrequency, ChargeFrequencyType ChargeFrequencyType, int ChargeFrequencyAmount,
    decimal? Amount, decimal? MinimumAmount, decimal? MaximumAmount, ChargePaymentMode ChargePaymentMode,
    bool Penalty, bool Override, int? GlAccountIncomeId);

// ---- Custom Fields & Values ----

public record CustomFieldResponse(
    int Id, string? Category, string? Name, CustomFieldType FieldType, bool Required,
    string? RadioBoxValues, string? CheckboxValues, string? SelectValues);

public record SaveCustomFieldRequest(
    string? Category, string? Name, CustomFieldType FieldType, bool Required,
    string? RadioBoxValues, string? CheckboxValues, string? SelectValues);

public record CustomFieldValueResponse(int Id, int CustomFieldId, string EntityType, int EntityId, string? Value);

public record CaptureCustomFieldValueRequest(int CustomFieldId, string EntityType, int EntityId, string? Value);
