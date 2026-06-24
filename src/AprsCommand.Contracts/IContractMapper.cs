namespace AprsCommand.Contracts;

public interface IContractMapper<in TInternal, TDto>
{
    ContractMappingResult<TDto> Map(TInternal value);
}
