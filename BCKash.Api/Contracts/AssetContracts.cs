using BCKash.Domain.Assets;

namespace BCKash.Api.Contracts;

public record SaveAssetTypeRequest(
    string? Name,
    int? GlAccountFixedAssetId,
    int? GlAccountAssetId,
    int? GlAccountContraAssetId,
    int? GlAccountExpenseId,
    int? GlAccountLiabilityId,
    int? GlAccountIncomeId,
    string? Notes);

public record AssetTypeResponse(
    int Id,
    string? Name,
    int? GlAccountFixedAssetId,
    int? GlAccountAssetId,
    int? GlAccountContraAssetId,
    int? GlAccountExpenseId,
    int? GlAccountLiabilityId,
    int? GlAccountIncomeId,
    string? Notes);

public record SaveAssetRequest(
    int? AssetTypeId,
    int? OfficeId,
    string? Name,
    DateOnly? PurchaseDate,
    decimal? PurchasePrice,
    int? LifeSpan,
    decimal? SalvageValue,
    string? SerialNumber,
    string? Notes,
    string? Files,
    string? PurchaseYear);

public record AssetResponse(
    int Id,
    int? AssetTypeId,
    int? OfficeId,
    string? Name,
    DateOnly? PurchaseDate,
    decimal? PurchasePrice,
    decimal? Value,
    int? LifeSpan,
    decimal? SalvageValue,
    string? SerialNumber,
    string? Notes,
    string? Files,
    string? PurchaseYear,
    AssetStatus? Status);

public record ChangeAssetStatusRequest(AssetStatus Status);

public record RunDepreciationRequest(string Year);

public record AssetDepreciationResponse(
    int Id,
    int? AssetId,
    string? Year,
    decimal? BeginningValue,
    decimal? DepreciationValue,
    decimal? Rate,
    decimal? Cost,
    decimal? Accumulated,
    decimal? EndingValue);
