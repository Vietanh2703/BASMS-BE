using Dapper;
using MassTransit;
using BuildingBlocks.Messaging.Events;

namespace Shifts.API.Validators;

/// <summary>
/// Validator để kiểm tra các ràng buộc về shift
/// </summary>
public class ShiftValidator
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IRequestClient<GetContractRequest> _contractRequestClient;
    private readonly ILogger<ShiftValidator> _logger;

    public ShiftValidator(
        IDbConnectionFactory dbFactory,
        IRequestClient<GetContractRequest> contractRequestClient,
        ILogger<ShiftValidator> logger)
    {
        _dbFactory = dbFactory;
        _contractRequestClient = contractRequestClient;
        _logger = logger;
    }

    /// <summary>
    /// Kiểm tra shift có bị trùng thời gian với các shift khác tại cùng location không
    /// </summary>
    /// <param name="locationId">ID địa điểm</param>
    /// <param name="shiftDate">Ngày ca trực</param>
    /// <param name="startTime">Giờ bắt đầu</param>
    /// <param name="endTime">Giờ kết thúc</param>
    /// <param name="excludeShiftId">ID shift cần loại trừ (dùng khi update)</param>
    /// <returns>ValidationResult chứa thông tin về shift bị trùng</returns>
    public async Task<ShiftOverlapValidationResult> ValidateShiftTimeOverlapAsync(
        Guid locationId,
        DateTime shiftDate,
        TimeSpan startTime,
        TimeSpan endTime,
        Guid? excludeShiftId = null)
    {
        try
        {
            _logger.LogInformation(
                "Validating shift time overlap for location {LocationId} on {ShiftDate:yyyy-MM-dd} {StartTime}-{EndTime}",
                locationId,
                shiftDate,
                startTime,
                endTime);

            using var connection = await _dbFactory.CreateConnectionAsync();

            var shiftStart = shiftDate.Date.Add(startTime);
            var shiftEnd = shiftDate.Date.Add(endTime);

            // Query để tìm shifts bị trùng thời gian
            // Trùng khi: (StartA < EndB) AND (EndA > StartB)
            var sql = @"
                SELECT
                    Id,
                    ShiftDate,
                    ShiftStart,
                    ShiftEnd,
                    RequiredGuards,
                    AssignedGuardsCount,
                    Status,
                    ContractId
                FROM shifts
                WHERE
                    LocationId = @LocationId
                    AND IsDeleted = 0
                    AND Status NOT IN ('CANCELLED', 'COMPLETED')
                    AND ShiftDate = @ShiftDate
                    AND (
                        (ShiftStart < @ShiftEnd AND ShiftEnd > @ShiftStart)
                    )
                    AND (@ExcludeShiftId IS NULL OR Id != @ExcludeShiftId)
                ORDER BY ShiftStart";

            var overlappingShifts = await connection.QueryAsync<OverlappingShift>(sql, new
            {
                LocationId = locationId,
                ShiftDate = shiftDate.Date,
                ShiftStart = shiftStart,
                ShiftEnd = shiftEnd,
                ExcludeShiftId = excludeShiftId
            });

            var overlappingShiftsList = overlappingShifts.ToList();

            if (overlappingShiftsList.Any())
            {
                _logger.LogWarning(
                    "Found {Count} overlapping shifts at location {LocationId}",
                    overlappingShiftsList.Count,
                    locationId);

                return new ShiftOverlapValidationResult
                {
                    IsValid = false,
                    HasOverlap = true,
                    ErrorMessage = $"Phát hiện {overlappingShiftsList.Count} ca trực trùng thời gian tại địa điểm này",
                    OverlappingShifts = overlappingShiftsList
                };
            }

            _logger.LogInformation("No overlapping shifts found");

            return new ShiftOverlapValidationResult
            {
                IsValid = true,
                HasOverlap = false,
                OverlappingShifts = new List<OverlappingShift>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating shift time overlap");
            throw;
        }
    }

    /// <summary>
    /// Kiểm tra shift có nằm trong khoảng thời gian của contract không
    /// </summary>
    /// <param name="contractId">ID hợp đồng</param>
    /// <param name="shiftDate">Ngày ca trực</param>
    /// <returns>ValidationResult</returns>
    public async Task<ContractPeriodValidationResult> ValidateShiftWithinContractPeriodAsync(
        Guid contractId,
        DateTime shiftDate)
    {
        try
        {
            _logger.LogInformation(
                "Validating shift date {ShiftDate:yyyy-MM-dd} within contract {ContractId} period",
                shiftDate,
                contractId);

            // Gửi request qua RabbitMQ để lấy thông tin contract từ Contracts.API
            var response = await _contractRequestClient.GetResponse<GetContractResponse>(
                new GetContractRequest { ContractId = contractId },
                timeout: RequestTimeout.After(s: 10));

            var contractResponse = response.Message;

            if (!contractResponse.Success || contractResponse.Contract == null)
            {
                _logger.LogWarning(
                    "Contract {ContractId} not found or invalid: {Error}",
                    contractId,
                    contractResponse.ErrorMessage);

                return new ContractPeriodValidationResult
                {
                    IsValid = false,
                    ErrorMessage = contractResponse.ErrorMessage ?? "Contract không tồn tại hoặc không hợp lệ"
                };
            }

            var contract = contractResponse.Contract;

            // Kiểm tra shift date có nằm trong khoảng StartDate và EndDate không
            if (shiftDate.Date < contract.StartDate.Date)
            {
                var message = $"Ngày ca trực ({shiftDate:yyyy-MM-dd}) trước ngày bắt đầu hợp đồng ({contract.StartDate:yyyy-MM-dd})";
                _logger.LogWarning(message);

                return new ContractPeriodValidationResult
                {
                    IsValid = false,
                    ErrorMessage = message,
                    ContractStartDate = contract.StartDate,
                    ContractEndDate = contract.EndDate
                };
            }

            if (shiftDate.Date > contract.EndDate.Date)
            {
                var message = $"Ngày ca trực ({shiftDate:yyyy-MM-dd}) sau ngày kết thúc hợp đồng ({contract.EndDate:yyyy-MM-dd})";
                _logger.LogWarning(message);

                return new ContractPeriodValidationResult
                {
                    IsValid = false,
                    ErrorMessage = message,
                    ContractStartDate = contract.StartDate,
                    ContractEndDate = contract.EndDate
                };
            }

            _logger.LogInformation(
                "Shift date is within contract period ({StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd})",
                contract.StartDate,
                contract.EndDate);

            return new ContractPeriodValidationResult
            {
                IsValid = true,
                ContractStartDate = contract.StartDate,
                ContractEndDate = contract.EndDate,
                ContractNumber = contract.ContractNumber
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating contract period");
            throw;
        }
    }
}

/// <summary>
/// Kết quả validation shift overlap
/// </summary>
public class ShiftOverlapValidationResult
{
    public bool IsValid { get; set; }
    public bool HasOverlap { get; set; }
    public string? ErrorMessage { get; set; }
    public List<OverlappingShift> OverlappingShifts { get; set; } = new();
}

/// <summary>
/// Thông tin shift bị trùng
/// </summary>
public class OverlappingShift
{
    public Guid Id { get; set; }
    public DateTime ShiftDate { get; set; }
    public DateTime ShiftStart { get; set; }
    public DateTime ShiftEnd { get; set; }
    public int RequiredGuards { get; set; }
    public int AssignedGuardsCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ContractId { get; set; }
}

/// <summary>
/// Kết quả validation contract period
/// </summary>
public class ContractPeriodValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public string? ContractNumber { get; set; }
}
