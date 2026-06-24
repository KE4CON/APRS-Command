namespace AprsCommand.Contracts;

public sealed record ContractMappingResult<TDto>(
    bool Success,
    TDto? Dto,
    ContractValidationResult Validation);
