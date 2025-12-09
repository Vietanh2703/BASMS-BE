using BuildingBlocks.CQRS;
using BuildingBlocks.Messaging.Events;
using Contracts.API.Data;
using Contracts.API.Models;
using Dapper;
using Dapper.Contrib.Extensions;
using MassTransit;

namespace Contracts.API.ContractsHandler.ActivateContract;

/// <summary>
/// Handler để activate contract và publish event cho Shifts.API
/// CRITICAL: Đây là điểm kích hoạt toàn bộ workflow tạo shifts
/// </summary>
public class ActivateContractHandler(
    IDbConnectionFactory connectionFactory,
    IPublishEndpoint publishEndpoint,
    ILogger<ActivateContractHandler> logger)
    : ICommandHandler<ActivateContractCommand, ActivateContractResult>
{
    public async Task<ActivateContractResult> Handle(
        ActivateContractCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Activating contract {ContractId}",
            request.ContractId);

        using var connection = await connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // ================================================================
            // BƯỚC 1: LẤY CONTRACT VÀ VALIDATE
            // ================================================================
            var contract = await connection.QueryFirstOrDefaultAsync<Contract>(
                "SELECT * FROM contracts WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.ContractId },
                transaction);

            if (contract == null)
            {
                return new ActivateContractResult
                {
                    Success = false,
                    ErrorMessage = $"Contract {request.ContractId} not found"
                };
            }

            // Check current status
            if (contract.Status == "active")
            {
                return new ActivateContractResult
                {
                    Success = false,
                    ErrorMessage = $"Contract {contract.ContractNumber} is already active"
                };
            }

            if (contract.Status == "terminated" || contract.Status == "expired")
            {
                return new ActivateContractResult
                {
                    Success = false,
                    ErrorMessage = $"Cannot activate {contract.Status} contract"
                };
            }

            logger.LogInformation(
                "Contract {ContractNumber} found - Current status: {Status}",
                contract.ContractNumber,
                contract.Status);

            // ================================================================
            // BƯỚC 2: LẤY THÔNG TIN LIÊN QUAN (CUSTOMER, LOCATIONS, SCHEDULES)
            // ================================================================

            // 2.1: Get Customer
            Customer? customer = null;
            if (contract.CustomerId.HasValue)
            {
                customer = await connection.QueryFirstOrDefaultAsync<Customer>(
                    "SELECT * FROM customers WHERE Id = @Id AND IsDeleted = 0",
                    new { Id = contract.CustomerId.Value },
                    transaction);
            }

            // 2.2: Get Contract Locations
            var contractLocations = await connection.QueryAsync<ContractLocation>(
                @"SELECT * FROM contract_locations
                  WHERE ContractId = @ContractId AND IsDeleted = 0",
                new { ContractId = contract.Id },
                transaction);

            var contractLocationsList = contractLocations.ToList();

            // 2.3: Get Location Details
            var locationDtos = new List<ContractLocationDto>();
            foreach (var contractLocation in contractLocationsList)
            {
                var location = await connection.QueryFirstOrDefaultAsync<CustomerLocation>(
                    "SELECT * FROM customer_locations WHERE Id = @Id AND IsDeleted = 0",
                    new { Id = contractLocation.LocationId },
                    transaction);

                if (location != null)
                {
                    locationDtos.Add(new ContractLocationDto
                    {
                        LocationId = location.Id,
                        LocationName = location.LocationName,
                        LocationAddress = location.Address ?? string.Empty,
                        LocationCode = location.LocationCode,
                        GuardsRequired = contractLocation.GuardsRequired,
                        CoverageType = contractLocation.CoverageType,
                        ServiceStartDate = contractLocation.ServiceStartDate,
                        ServiceEndDate = contractLocation.ServiceEndDate,
                        Latitude = location.Latitude,
                        Longitude = location.Longitude,
                        GeofenceRadiusMeters = location.GeofenceRadiusMeters
                    });
                }
            }

            // 2.4: Get Shift Schedules
            var schedules = await connection.QueryAsync<ContractShiftSchedule>(
                @"SELECT * FROM contract_shift_schedules
                  WHERE ContractId = @ContractId AND IsDeleted = 0 AND IsActive = 1",
                new { ContractId = contract.Id },
                transaction);

            var scheduleDtos = schedules.Select(s => new ContractShiftScheduleDto
            {
                ScheduleId = s.Id,
                ScheduleName = s.ScheduleName,
                ScheduleType = s.ScheduleType,
                LocationId = s.LocationId,
                ShiftStartTime = s.ShiftStartTime,
                ShiftEndTime = s.ShiftEndTime,
                CrossesMidnight = s.CrossesMidnight,
                DurationHours = s.DurationHours,
                BreakMinutes = s.BreakMinutes,
                GuardsPerShift = s.GuardsPerShift,
                RecurrenceType = s.RecurrenceType,
                AppliesMonday = s.AppliesMonday,
                AppliesTuesday = s.AppliesTuesday,
                AppliesWednesday = s.AppliesWednesday,
                AppliesThursday = s.AppliesThursday,
                AppliesFriday = s.AppliesFriday,
                AppliesSaturday = s.AppliesSaturday,
                AppliesSunday = s.AppliesSunday,
                AppliesOnPublicHolidays = s.AppliesOnPublicHolidays,
                AppliesOnWeekends = s.AppliesOnWeekends,
                SkipWhenLocationClosed = s.SkipWhenLocationClosed,
                RequiresArmedGuard = s.RequiresArmedGuard,
                RequiresSupervisor = s.RequiresSupervisor,
                MinimumExperienceMonths = s.MinimumExperienceMonths,
                EffectiveFrom = s.EffectiveFrom,
                EffectiveTo = s.EffectiveTo
            }).ToList();

            logger.LogInformation(
                @"Contract details loaded:
                  - Customer: {CustomerName}
                  - Locations: {LocationCount}
                  - Shift Schedules: {ScheduleCount}
                  - Auto Generate: {AutoGenerate}",
                customer?.CompanyName ?? "N/A",
                locationDtos.Count,
                scheduleDtos.Count,
                contract.AutoGenerateShifts);

            // ================================================================
            // BƯỚC 3: VALIDATE TRƯỚC KHI ACTIVATE
            // ================================================================
            var validationErrors = new List<string>();

            if (locationDtos.Count == 0)
            {
                validationErrors.Add("Contract must have at least one location");
            }

            if (scheduleDtos.Count == 0)
            {
                validationErrors.Add("Contract must have at least one shift schedule");
            }

            if (contract.StartDate > contract.EndDate)
            {
                validationErrors.Add("Contract start date must be before end date");
            }

            if (validationErrors.Any())
            {
                logger.LogWarning(
                    "Contract validation failed: {Errors}",
                    string.Join(", ", validationErrors));

                return new ActivateContractResult
                {
                    Success = false,
                    ErrorMessage = $"Validation failed: {string.Join(", ", validationErrors)}"
                };
            }

            // ================================================================
            // BƯỚC 4: UPDATE CONTRACT STATUS → SCHEDULE_SHIFTS
            // ================================================================
            contract.Status = "schedule_shifts";
            contract.ActivatedAt = DateTime.UtcNow;
            contract.UpdatedAt = DateTime.UtcNow;
            contract.UpdatedBy = request.ActivatedBy;

            // Nếu chưa có approved, set luôn
            if (!contract.ApprovedAt.HasValue)
            {
                contract.ApprovedAt = DateTime.UtcNow;
                contract.ApprovedBy = request.ActivatedBy;
            }

            await connection.UpdateAsync(contract, transaction);

            logger.LogInformation(
                "✓ Contract {ContractNumber} status updated to SCHEDULE_SHIFTS",
                contract.ContractNumber);

            // ================================================================
            // BƯỚC 5: PUBLISH ContractActivatedEvent → SHIFTS.API
            // ================================================================
            var activatedEvent = new ContractActivatedEvent
            {
                ContractId = contract.Id,
                ContractNumber = contract.ContractNumber,
                ContractTitle = contract.ContractTitle,
                CustomerId = contract.CustomerId ?? Guid.Empty,
                CustomerName = customer?.CompanyName ?? "N/A",
                ManagerId = request.ManagerId,
                StartDate = contract.StartDate,
                EndDate = contract.EndDate,
                AutoGenerateShifts = contract.AutoGenerateShifts,
                GenerateShiftsAdvanceDays = contract.GenerateShiftsAdvanceDays,
                WorkOnPublicHolidays = contract.WorkOnPublicHolidays,
                WorkOnCustomerClosedDays = contract.WorkOnCustomerClosedDays,
                Locations = locationDtos,
                ShiftSchedules = scheduleDtos,
                ActivatedAt = contract.ActivatedAt ?? DateTime.UtcNow,
                ActivatedBy = request.ActivatedBy
            };

            await publishEndpoint.Publish(activatedEvent, cancellationToken);

            logger.LogInformation(
                @"✓ ContractActivatedEvent published for {ContractNumber}:
                  - Event sent to Shifts.API via RabbitMQ
                  - Locations: {LocationCount}
                  - Schedules: {ScheduleCount}
                  - Auto Generate: {AutoGenerate}",
                contract.ContractNumber,
                locationDtos.Count,
                scheduleDtos.Count,
                contract.AutoGenerateShifts);

            // ================================================================
            // BƯỚC 6: COMMIT TRANSACTION
            // ================================================================
            transaction.Commit();

            logger.LogInformation(
                "✓✓✓ Contract {ContractNumber} activated successfully!",
                contract.ContractNumber);

            return new ActivateContractResult
            {
                Success = true,
                ContractId = contract.Id,
                ContractNumber = contract.ContractNumber,
                Status = contract.Status,
                ActivatedAt = contract.ActivatedAt,
                ActivationInfo = new ContractActivationInfo
                {
                    LocationsCount = locationDtos.Count,
                    ShiftSchedulesCount = scheduleDtos.Count,
                    AutoGenerateShifts = contract.AutoGenerateShifts,
                    GenerateShiftsAdvanceDays = contract.GenerateShiftsAdvanceDays,
                    StartDate = contract.StartDate,
                    EndDate = contract.EndDate,
                    CustomerName = customer?.CompanyName,
                    EventPublished = true
                }
            };
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            logger.LogError(ex,
                "Failed to activate contract {ContractId}",
                request.ContractId);

            return new ActivateContractResult
            {
                Success = false,
                ErrorMessage = $"Activation failed: {ex.Message}"
            };
        }
    }
}
