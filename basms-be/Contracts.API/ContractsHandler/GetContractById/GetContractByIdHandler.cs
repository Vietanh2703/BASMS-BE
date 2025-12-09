namespace Contracts.API.ContractsHandler.GetContractById;

// ================================================================
// QUERY & RESULT
// ================================================================

/// <summary>
/// Query để lấy thông tin chi tiết Contract theo ID
/// </summary>
public record GetContractByIdQuery(Guid ContractId) : IQuery<GetContractByIdResult>;

/// <summary>
/// Kết quả chi tiết contract với tất cả thông tin liên quan
/// </summary>
public record GetContractByIdResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public ContractDetailDto? Contract { get; init; }
}

/// <summary>
/// DTO chi tiết contract
/// </summary>
public record ContractDetailDto
{
    // Contract info
    public Guid Id { get; init; }
    public string ContractNumber { get; init; } = string.Empty;
    public string ContractTitle { get; init; } = string.Empty;
    public string ContractType { get; init; } = string.Empty;
    public string ServiceScope { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int DurationMonths { get; init; }
    public string Status { get; init; } = string.Empty;
    public string CoverageModel { get; init; } = string.Empty;

    // Renewal info
    public bool IsRenewable { get; init; }
    public bool AutoRenewal { get; init; }
    public int RenewalNoticeDays { get; init; }
    public int RenewalCount { get; init; }

    // Schedule settings
    public bool FollowsCustomerCalendar { get; init; }
    public bool WorkOnPublicHolidays { get; init; }
    public bool WorkOnCustomerClosedDays { get; init; }
    public bool AutoGenerateShifts { get; init; }
    public int GenerateShiftsAdvanceDays { get; init; }

    // Related data
    public CustomerDto? Customer { get; init; }
    public List<ContractDocumentDto> Documents { get; init; } = new();
    public List<ContractLocationDto> Locations { get; init; } = new();
    public List<ContractShiftScheduleDto> ShiftSchedules { get; init; } = new();
    public List<ContractPeriodDto> Periods { get; init; } = new();
    public List<PublicHolidayDto> PublicHolidays { get; init; } = new();

    // Metadata
    public Guid? CreatedBy { get; init; }
    public DateTime CreatedAt { get; init; }
    public Guid? UpdatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// DTO Customer
/// </summary>
public record CustomerDto
{
    public Guid Id { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string ContactPersonName { get; init; } = string.Empty;
    public string? ContactPersonTitle { get; init; }
    public string IdentityNumber { get; init; } = string.Empty;
    public DateTime? IdentityIssueDate { get; init; }
    public string? IdentityIssuePlace { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public DateTime DateOfBirth { get; init; }
    public string Address { get; init; } = string.Empty;
    public string? City { get; init; }
    public string? District { get; init; }
    public string? Industry { get; init; }
    public string Status { get; init; } = string.Empty;
}

/// <summary>
/// DTO Contract Document
/// </summary>
public record ContractDocumentDto
{
    public Guid Id { get; init; }
    public string DocumentType { get; init; } = string.Empty;
    public string DocumentName { get; init; } = string.Empty;
    public string FileUrl { get; init; } = string.Empty;
    public long? FileSize { get; init; }
    public string? MimeType { get; init; }
    public string Version { get; init; } = string.Empty;
    public DateTime? DocumentDate { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// DTO Contract Location with Customer Location details
/// </summary>
public record ContractLocationDto
{
    // ContractLocation info
    public Guid Id { get; init; }
    public Guid LocationId { get; init; }
    public int GuardsRequired { get; init; }
    public string CoverageType { get; init; } = string.Empty;
    public DateTime ServiceStartDate { get; init; }
    public DateTime? ServiceEndDate { get; init; }
    public bool IsPrimaryLocation { get; init; }
    public int PriorityLevel { get; init; }
    public bool AutoGenerateShifts { get; init; }
    public bool IsActive { get; init; }
    public string? Notes { get; init; }

    // CustomerLocation details
    public CustomerLocationDetailsDto? LocationDetails { get; init; }
}

/// <summary>
/// DTO Customer Location details
/// </summary>
public record CustomerLocationDetailsDto
{
    public string LocationCode { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public string LocationType { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string? City { get; init; }
    public string? District { get; init; }
    public string? Ward { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public int GeofenceRadiusMeters { get; init; }
    public string? SiteManagerName { get; init; }
    public string? SiteManagerPhone { get; init; }
}

/// <summary>
/// DTO Contract Shift Schedule
/// </summary>
public record ContractShiftScheduleDto
{
    public Guid Id { get; init; }
    public Guid? LocationId { get; init; }
    public string ScheduleName { get; init; } = string.Empty;
    public string ScheduleType { get; init; } = string.Empty;

    // Time
    public TimeSpan ShiftStartTime { get; init; }
    public TimeSpan ShiftEndTime { get; init; }
    public bool CrossesMidnight { get; init; }
    public decimal DurationHours { get; init; }
    public int BreakMinutes { get; init; }

    // Staff
    public int GuardsPerShift { get; init; }

    // Recurrence
    public string RecurrenceType { get; init; } = string.Empty;
    public bool AppliesMonday { get; init; }
    public bool AppliesTuesday { get; init; }
    public bool AppliesWednesday { get; init; }
    public bool AppliesThursday { get; init; }
    public bool AppliesFriday { get; init; }
    public bool AppliesSaturday { get; init; }
    public bool AppliesSunday { get; init; }
    public bool AppliesOnPublicHolidays { get; init; }
    public bool AppliesOnCustomerHolidays { get; init; }
    public bool AppliesOnWeekends { get; init; }
    public bool SkipWhenLocationClosed { get; init; }

    // Requirements
    public bool RequiresArmedGuard { get; init; }
    public bool RequiresSupervisor { get; init; }
    public int MinimumExperienceMonths { get; init; }

    // Auto generation
    public bool AutoGenerateEnabled { get; init; }
    public int GenerateAdvanceDays { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>
/// DTO Contract Period
/// </summary>
public record ContractPeriodDto
{
    public Guid Id { get; init; }
    public int PeriodNumber { get; init; }
    public string PeriodType { get; init; } = string.Empty;
    public DateTime PeriodStartDate { get; init; }
    public DateTime PeriodEndDate { get; init; }
    public bool IsCurrentPeriod { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// DTO Public Holiday
/// </summary>
public record PublicHolidayDto
{
    public Guid Id { get; init; }
    public DateTime HolidayDate { get; init; }
    public string HolidayName { get; init; } = string.Empty;
    public string? HolidayNameEn { get; init; }
    public string HolidayCategory { get; init; } = string.Empty;
    public bool IsTetPeriod { get; init; }
    public bool IsTetHoliday { get; init; }
    public int? TetDayNumber { get; init; }
    public DateTime? HolidayStartDate { get; init; }
    public DateTime? HolidayEndDate { get; init; }
    public int? TotalHolidayDays { get; init; }
    public bool IsOfficialHoliday { get; init; }
    public bool IsObserved { get; init; }
    public DateTime? OriginalDate { get; init; }
    public DateTime? ObservedDate { get; init; }
    public bool AppliesNationwide { get; init; }
    public string? AppliesToRegions { get; init; }
    public bool StandardWorkplacesClosed { get; init; }
    public bool EssentialServicesOperating { get; init; }
    public string? Description { get; init; }
    public int Year { get; init; }
}


// ================================================================
// HANDLER
// ================================================================

internal class GetContractByIdHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetContractByIdHandler> logger)
    : IQueryHandler<GetContractByIdQuery, GetContractByIdResult>
{
    public async Task<GetContractByIdResult> Handle(
        GetContractByIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting contract details for ContractId: {ContractId}", request.ContractId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // ================================================================
            // 1. LẤY CONTRACT
            // ================================================================
            var contract = await connection.QueryFirstOrDefaultAsync<Models.Contract>(
                "SELECT * FROM contracts WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.ContractId });

            if (contract == null)
            {
                return new GetContractByIdResult
                {
                    Success = false,
                    ErrorMessage = $"Contract with ID {request.ContractId} not found"
                };
            }

            // ================================================================
            // 2. LẤY CUSTOMER (null for working_contract)
            // ================================================================
            Models.Customer? customer = null;
            if (contract.CustomerId.HasValue)
            {
                customer = await connection.QueryFirstOrDefaultAsync<Models.Customer>(
                    "SELECT * FROM customers WHERE Id = @CustomerId AND IsDeleted = 0",
                    new { CustomerId = contract.CustomerId.Value });
            }

            // ================================================================
            // 3. LẤY CONTRACT DOCUMENT (chỉ 1 document chính)
            // ================================================================
            Models.ContractDocument? document = null;
            if (contract.DocumentId.HasValue)
            {
                document = await connection.QueryFirstOrDefaultAsync<Models.ContractDocument>(
                    "SELECT * FROM contract_documents WHERE Id = @Id AND IsDeleted = 0",
                    new { Id = contract.DocumentId.Value });
            }

            var documents = document != null ? new[] { document } : Array.Empty<Models.ContractDocument>();

            // ================================================================
            // 4. LẤY CONTRACT LOCATIONS + CUSTOMER LOCATION DETAILS
            // ================================================================
            var contractLocations = await connection.QueryAsync<Models.ContractLocation>(
                "SELECT * FROM contract_locations WHERE ContractId = @ContractId AND IsDeleted = 0",
                new { ContractId = contract.Id });

            var locationDtos = new List<ContractLocationDto>();
            foreach (var contractLocation in contractLocations)
            {
                var customerLocation = await connection.QueryFirstOrDefaultAsync<Models.CustomerLocation>(
                    "SELECT * FROM customer_locations WHERE Id = @LocationId AND IsDeleted = 0",
                    new { LocationId = contractLocation.LocationId });

                locationDtos.Add(new ContractLocationDto
                {
                    Id = contractLocation.Id,
                    LocationId = contractLocation.LocationId,
                    GuardsRequired = contractLocation.GuardsRequired,
                    CoverageType = contractLocation.CoverageType,
                    ServiceStartDate = contractLocation.ServiceStartDate,
                    ServiceEndDate = contractLocation.ServiceEndDate,
                    IsPrimaryLocation = contractLocation.IsPrimaryLocation,
                    PriorityLevel = contractLocation.PriorityLevel,
                    AutoGenerateShifts = contractLocation.AutoGenerateShifts,
                    IsActive = contractLocation.IsActive,
                    Notes = contractLocation.Notes,
                    LocationDetails = customerLocation != null ? new CustomerLocationDetailsDto
                    {
                        LocationCode = customerLocation.LocationCode,
                        LocationName = customerLocation.LocationName,
                        LocationType = customerLocation.LocationType,
                        Address = customerLocation.Address,
                        City = customerLocation.City,
                        District = customerLocation.District,
                        Ward = customerLocation.Ward,
                        Latitude = customerLocation.Latitude,
                        Longitude = customerLocation.Longitude,
                        GeofenceRadiusMeters = customerLocation.GeofenceRadiusMeters,
                        SiteManagerName = customerLocation.SiteManagerName,
                        SiteManagerPhone = customerLocation.SiteManagerPhone
                    } : null
                });
            }

            // ================================================================
            // 5. LẤY CONTRACT SHIFT SCHEDULES
            // ================================================================
            var shiftSchedules = await connection.QueryAsync<Models.ContractShiftSchedule>(
                "SELECT * FROM contract_shift_schedules WHERE ContractId = @ContractId AND IsDeleted = 0 ORDER BY ShiftStartTime",
                new { ContractId = contract.Id });

            // ================================================================
            // 6. LẤY CONTRACT PERIODS
            // ================================================================
            var periods = await connection.QueryAsync<Models.ContractPeriod>(
                "SELECT * FROM contract_periods WHERE ContractId = @ContractId ORDER BY PeriodNumber DESC",
                new { ContractId = contract.Id });

            // ================================================================
            // 7. LẤY PUBLIC HOLIDAYS
            // ================================================================
            var publicHolidays = await connection.QueryAsync<Models.PublicHoliday>(
                "SELECT * FROM public_holidays WHERE ContractId = @ContractId ORDER BY HolidayDate ASC",
                new { ContractId = contract.Id });

            // ================================================================
            // 8. MAP TO DTOS
            // ================================================================
            var result = new GetContractByIdResult
            {
                Success = true,
                Contract = new ContractDetailDto
                {
                    Id = contract.Id,
                    ContractNumber = contract.ContractNumber,
                    ContractTitle = contract.ContractTitle,
                    ContractType = contract.ContractType,
                    ServiceScope = contract.ServiceScope,
                    StartDate = contract.StartDate,
                    EndDate = contract.EndDate,
                    DurationMonths = contract.DurationMonths,
                    Status = contract.Status,
                    CoverageModel = contract.CoverageModel,
                    IsRenewable = contract.IsRenewable,
                    AutoRenewal = contract.AutoRenewal,
                    RenewalNoticeDays = contract.RenewalNoticeDays,
                    RenewalCount = contract.RenewalCount,
                    FollowsCustomerCalendar = contract.FollowsCustomerCalendar,
                    WorkOnPublicHolidays = contract.WorkOnPublicHolidays,
                    WorkOnCustomerClosedDays = contract.WorkOnCustomerClosedDays,
                    AutoGenerateShifts = contract.AutoGenerateShifts,
                    GenerateShiftsAdvanceDays = contract.GenerateShiftsAdvanceDays,
                    CreatedBy = contract.CreatedBy,
                    CreatedAt = contract.CreatedAt,
                    UpdatedBy = contract.UpdatedBy,
                    UpdatedAt = contract.UpdatedAt,

                    Customer = customer != null ? new CustomerDto
                    {
                        Id = customer.Id,
                        CustomerCode = customer.CustomerCode,
                        CompanyName = customer.CompanyName,
                        ContactPersonName = customer.ContactPersonName,
                        ContactPersonTitle = customer.ContactPersonTitle,
                        IdentityNumber = customer.IdentityNumber,
                        IdentityIssueDate = customer.IdentityIssueDate,
                        IdentityIssuePlace = customer.IdentityIssuePlace,
                        Email = customer.Email,
                        Phone = customer.Phone,
                        DateOfBirth = customer.DateOfBirth,
                        Address = customer.Address,
                        City = customer.City,
                        District = customer.District,
                        Industry = customer.Industry,
                        Status = customer.Status
                    } : null,

                    Documents = documents.Select(d => new ContractDocumentDto
                    {
                        Id = d.Id,
                        DocumentType = d.DocumentType,
                        DocumentName = d.DocumentName,
                        FileUrl = d.FileUrl,
                        FileSize = d.FileSize,
                        Version = d.Version,
                        DocumentDate = d.DocumentDate,
                        CreatedAt = d.CreatedAt
                    }).ToList(),

                    Locations = locationDtos,

                    ShiftSchedules = shiftSchedules.Select(s => new ContractShiftScheduleDto
                    {
                        Id = s.Id,
                        LocationId = s.LocationId,
                        ScheduleName = s.ScheduleName,
                        ScheduleType = s.ScheduleType,
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
                        AppliesOnCustomerHolidays = s.AppliesOnCustomerHolidays,
                        AppliesOnWeekends = s.AppliesOnWeekends,
                        SkipWhenLocationClosed = s.SkipWhenLocationClosed,
                        RequiresArmedGuard = s.RequiresArmedGuard,
                        RequiresSupervisor = s.RequiresSupervisor,
                        MinimumExperienceMonths = s.MinimumExperienceMonths,
                        AutoGenerateEnabled = s.AutoGenerateEnabled,
                        GenerateAdvanceDays = s.GenerateAdvanceDays,
                        EffectiveFrom = s.EffectiveFrom,
                        EffectiveTo = s.EffectiveTo,
                        IsActive = s.IsActive
                    }).ToList(),

                    Periods = periods.Select(p => new ContractPeriodDto
                    {
                        Id = p.Id,
                        PeriodNumber = p.PeriodNumber,
                        PeriodType = p.PeriodType,
                        PeriodStartDate = p.PeriodStartDate,
                        PeriodEndDate = p.PeriodEndDate,
                        IsCurrentPeriod = p.IsCurrentPeriod,
                        Notes = p.Notes,
                        CreatedAt = p.CreatedAt
                    }).ToList(),

                    PublicHolidays = publicHolidays.Select(h => new PublicHolidayDto
                    {
                        Id = h.Id,
                        HolidayDate = h.HolidayDate,
                        HolidayName = h.HolidayName,
                        HolidayNameEn = h.HolidayNameEn,
                        HolidayCategory = h.HolidayCategory,
                        IsTetPeriod = h.IsTetPeriod,
                        IsTetHoliday = h.IsTetHoliday,
                        TetDayNumber = h.TetDayNumber,
                        HolidayStartDate = h.HolidayStartDate,
                        HolidayEndDate = h.HolidayEndDate,
                        TotalHolidayDays = h.TotalHolidayDays,
                        IsOfficialHoliday = h.IsOfficialHoliday,
                        IsObserved = h.IsObserved,
                        OriginalDate = h.OriginalDate,
                        ObservedDate = h.ObservedDate,
                        AppliesNationwide = h.AppliesNationwide,
                        AppliesToRegions = h.AppliesToRegions,
                        StandardWorkplacesClosed = h.StandardWorkplacesClosed,
                        EssentialServicesOperating = h.EssentialServicesOperating,
                        Description = h.Description,
                        Year = h.Year
                    }).ToList(),
                }
            };

            logger.LogInformation(
                "Contract details retrieved: {ContractNumber} - {Locations} locations, {Schedules} schedules, {Documents} documents, {Holidays} public holidays",
                contract.ContractNumber, locationDtos.Count, shiftSchedules.Count(), documents.Count(), publicHolidays.Count());

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting contract details for ContractId: {ContractId}", request.ContractId);
            return new GetContractByIdResult
            {
                Success = false,
                ErrorMessage = $"Error retrieving contract: {ex.Message}"
            };
        }
    }
}
