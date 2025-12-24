namespace Shifts.API.GuardsHandler.GetAllWageRates;

public record GetAllWageRatesQuery() : IQuery<GetAllWageRatesResult>;
public record GetAllWageRatesResult(IEnumerable<WageRateDto> WageRates);

public record WageRateDto(
    Guid Id,
    string CertificationLevel,
    decimal MinWage,
    decimal MaxWage,
    decimal StandardWage,
    string? StandardWageInWords,
    string Currency,
    string? Description,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    string? Notes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

internal class GetAllWageRatesHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetAllWageRatesHandler> logger)
    : IQueryHandler<GetAllWageRatesQuery, GetAllWageRatesResult>
{
    public async Task<GetAllWageRatesResult> Handle(GetAllWageRatesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting all wage rates from database");

            using var connection = await connectionFactory.CreateConnectionAsync();
            var wageRates = await connection.GetAllAsync<WageRates>();
            var wageRateDtos = wageRates
                .Where(w => w.IsActive)
                .Select(w => new WageRateDto(
                    Id: w.Id,
                    CertificationLevel: w.CertificationLevel,
                    MinWage: w.MinWage,
                    MaxWage: w.MaxWage,
                    StandardWage: w.StandardWage,
                    StandardWageInWords: w.StandardWageInWords,
                    Currency: w.Currency,
                    Description: w.Description,
                    EffectiveFrom: w.EffectiveFrom,
                    EffectiveTo: w.EffectiveTo,
                    Notes: w.Notes,
                    IsActive: w.IsActive,
                    CreatedAt: w.CreatedAt,
                    UpdatedAt: w.UpdatedAt
                ))
                .OrderBy(w => w.CertificationLevel)
                .ThenByDescending(w => w.EffectiveFrom)
                .ToList();

            logger.LogInformation("Successfully retrieved {Count} wage rates", wageRateDtos.Count);

            return new GetAllWageRatesResult(wageRateDtos);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting wage rates from database");
            throw;
        }
    }
}
