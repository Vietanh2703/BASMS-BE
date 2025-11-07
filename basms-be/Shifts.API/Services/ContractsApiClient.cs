using System.Text.Json;

namespace Shifts.API.Services;

/// <summary>
/// HTTP Client để gọi Contracts.API
/// Wrap các HTTP calls và deserialize responses
/// </summary>
public class ContractsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ContractsApiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ContractsApiClient(HttpClient httpClient, ILogger<ContractsApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// GET /api/contracts/{id}/validate
    /// </summary>
    public async Task<ContractValidationResponse?> ValidateContractAsync(Guid contractId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/contracts/{contractId}/validate");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to validate contract {ContractId}: {StatusCode}",
                    contractId,
                    response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ContractValidationResponse>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Contracts.API to validate contract {ContractId}", contractId);
            return null;
        }
    }

    /// <summary>
    /// GET /api/contracts/{contractId}/locations/{locationId}/validate
    /// </summary>
    public async Task<LocationValidationResponse?> ValidateLocationAsync(Guid contractId, Guid locationId)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/contracts/{contractId}/locations/{locationId}/validate");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to validate location {LocationId} for contract {ContractId}: {StatusCode}",
                    locationId,
                    contractId,
                    response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<LocationValidationResponse>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error calling Contracts.API to validate location {LocationId} for contract {ContractId}",
                locationId,
                contractId);
            return null;
        }
    }

    /// <summary>
    /// GET /api/holidays/check?date={date}
    /// </summary>
    public async Task<HolidayCheckResponse?> CheckPublicHolidayAsync(DateTime date)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/holidays/check?date={date:yyyy-MM-dd}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to check holiday for {Date}: {StatusCode}", date, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<HolidayCheckResponse>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Contracts.API to check holiday for {Date}", date);
            return null;
        }
    }

    /// <summary>
    /// GET /api/locations/{locationId}/check-closed?date={date}
    /// </summary>
    public async Task<LocationClosedResponse?> CheckLocationClosedAsync(Guid locationId, DateTime date)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/locations/{locationId}/check-closed?date={date:yyyy-MM-dd}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to check if location {LocationId} is closed on {Date}: {StatusCode}",
                    locationId,
                    date,
                    response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<LocationClosedResponse>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error calling Contracts.API to check if location {LocationId} is closed on {Date}",
                locationId,
                date);
            return null;
        }
    }
}

// ============================================================================
// RESPONSE DTOs (match Contracts.API responses)
// ============================================================================

public record ContractValidationResponse(
    bool IsValid,
    bool Exists,
    bool IsActive,
    string? ErrorMessage,
    ContractDto? Contract
);

public record ContractDto(
    Guid Id,
    string ContractNumber,
    string ContractTitle,
    Guid CustomerId,
    string CustomerName,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    bool WorkOnPublicHolidays,
    bool WorkOnCustomerClosedDays,
    bool AutoGenerateShifts
);

public record LocationValidationResponse(
    bool IsValid,
    bool Exists,
    bool IsActive,
    bool BelongsToContract,
    string? ErrorMessage,
    LocationDto? Location
);

public record LocationDto(
    Guid Id,
    string LocationCode,
    string LocationName,
    string LocationType,
    string Address,
    decimal? Latitude,
    decimal? Longitude,
    int GeofenceRadiusMeters,
    bool IsActive,
    int MinimumGuardsRequired
);

public record HolidayCheckResponse(
    bool IsHoliday,
    string? HolidayName,
    string? HolidayCategory,
    bool IsTetPeriod
);

public record LocationClosedResponse(
    bool IsClosed,
    string? Reason,
    string? DayType
);
