namespace Contracts.API.ContractsHandler.GetCustomerById;

public record GetCustomerByIdQuery(Guid CustomerId) : IQuery<GetCustomerByIdResult>;

public record CustomerDetailDto
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
    public string? AvatarUrl { get; init; }
    public string? Gender { get; init; }
    public DateTime DateOfBirth { get; init; }
    public string Address { get; init; } = string.Empty;
    public string? City { get; init; }
    public string? District { get; init; }
    public string? Industry { get; init; }
    public string? CompanySize { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CustomerSince { get; init; }
    public bool FollowsNationalHolidays { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
}


public record CustomerLocationDto
{
    public Guid Id { get; init; }
    public Guid CustomerId { get; init; }
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
    public string OperatingHoursType { get; init; } = string.Empty;
    public bool Requires24x7Coverage { get; init; }
    public int MinimumGuardsRequired { get; init; }
    public bool IsActive { get; init; }
}

public record ContractDocumentDto
{
    public Guid Id { get; init; }
    public string DocumentType { get; init; } = string.Empty;
    public string? Category { get; init; }
    public string DocumentName { get; init; } = string.Empty;
    public string FileUrl { get; init; } = string.Empty;
    public long? FileSize { get; init; }
    public string Version { get; init; } = string.Empty;
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public DateTime? SignDate { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public DateTime CreatedAt { get; init; }

    public string FileSizeFormatted => FileSize.HasValue
        ? FormatFileSize(FileSize.Value)
        : "Unknown";

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

public record ContractLocationDto
{
    public Guid Id { get; init; }
    public Guid ContractId { get; init; }
    public Guid LocationId { get; init; }
    public int GuardsRequired { get; init; }
    public string CoverageType { get; init; } = string.Empty;
    public DateTime ServiceStartDate { get; init; }
    public DateTime? ServiceEndDate { get; init; }
    public bool IsPrimaryLocation { get; init; }
    public int PriorityLevel { get; init; }
    public bool AutoGenerateShifts { get; init; }
    public bool IsActive { get; init; }
}

public record ContractShiftScheduleDto
{
    public Guid Id { get; init; }
    public Guid ContractId { get; init; }
    public Guid? LocationId { get; init; }
    public string ScheduleName { get; init; } = string.Empty;
    public string ScheduleType { get; init; } = string.Empty;
    public TimeSpan ShiftStartTime { get; init; }
    public TimeSpan ShiftEndTime { get; init; }
    public bool CrossesMidnight { get; init; }
    public decimal DurationHours { get; init; }
    public int BreakMinutes { get; init; }
    public int GuardsPerShift { get; init; }
    public string RecurrenceType { get; init; } = string.Empty;
    public bool AppliesMonday { get; init; }
    public bool AppliesTuesday { get; init; }
    public bool AppliesWednesday { get; init; }
    public bool AppliesThursday { get; init; }
    public bool AppliesFriday { get; init; }
    public bool AppliesSaturday { get; init; }
    public bool AppliesSunday { get; init; }
    public string? MonthlyDates { get; init; }
    public bool AppliesOnPublicHolidays { get; init; }
    public bool AppliesOnCustomerHolidays { get; init; }
    public bool AppliesOnWeekends { get; init; }
    public bool SkipWhenLocationClosed { get; init; }
    public bool RequiresArmedGuard { get; init; }
    public bool RequiresSupervisor { get; init; }
    public int MinimumExperienceMonths { get; init; }
    public string? RequiredCertifications { get; init; }
    public bool AutoGenerateEnabled { get; init; }
    public int GenerateAdvanceDays { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public bool IsActive { get; init; }
    public string? Notes { get; init; }
    public Guid? CreatedBy { get; init; }
}

public record PublicHolidayDto
{
    public Guid Id { get; init; }
    public Guid? ContractId { get; init; }
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

public record ContractDto
{
    public Guid Id { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? DocumentId { get; init; }
    public string ContractNumber { get; init; } = string.Empty;
    public string ContractTitle { get; init; } = string.Empty;
    public string ContractType { get; init; } = string.Empty;
    public string ServiceScope { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int DurationMonths { get; init; }
    public string CoverageModel { get; init; } = string.Empty;
    public bool FollowsCustomerCalendar { get; init; }
    public bool WorkOnPublicHolidays { get; init; }
    public bool WorkOnCustomerClosedDays { get; init; }
    public bool IsRenewable { get; init; }
    public bool AutoRenewal { get; init; }
    public int RenewalNoticeDays { get; init; }
    public int RenewalCount { get; init; }
    public bool AutoGenerateShifts { get; init; }
    public int GenerateShiftsAdvanceDays { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid? ApprovedBy { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public DateTime? SignedDate { get; init; }
    public DateTime? ActivatedAt { get; init; }
    public DateTime? TerminationDate { get; init; }
    public string? TerminationType { get; init; }
    public string? TerminationReason { get; init; }
    public Guid? TerminatedBy { get; init; }
    public string? ContractFileUrl { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    public ContractDocumentDto? Document { get; init; }
    public List<ContractLocationDto> Locations { get; init; } = new();
    public List<ContractShiftScheduleDto> ShiftSchedules { get; init; } = new();
    public List<PublicHolidayDto> PublicHolidays { get; init; } = new();
}


public record GetCustomerByIdResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public CustomerDetailDto? Customer { get; init; }
    public List<CustomerLocationDto> Locations { get; init; } = new();
    public List<ContractDto> Contracts { get; init; } = new();
}

internal class GetCustomerByIdHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetCustomerByIdHandler> logger)
    : IQueryHandler<GetCustomerByIdQuery, GetCustomerByIdResult>
{
    public async Task<GetCustomerByIdResult> Handle(
        GetCustomerByIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting customer detail for ID: {CustomerId}", request.CustomerId);

            using var connection = await connectionFactory.CreateConnectionAsync();
            
            var customerQuery = @"
                SELECT
                    Id, CustomerCode, CompanyName, ContactPersonName, ContactPersonTitle,
                    IdentityNumber, IdentityIssueDate, IdentityIssuePlace,
                    Email, Phone, AvatarUrl, Gender, DateOfBirth,
                    Address, City, District, Industry, CompanySize,
                    Status, CustomerSince, FollowsNationalHolidays, Notes, CreatedAt
                FROM customers
                WHERE Id = @CustomerId AND IsDeleted = 0
            ";

            var customer = await connection.QuerySingleOrDefaultAsync<Customer>(
                customerQuery,
                new { request.CustomerId });

            if (customer == null)
            {
                logger.LogWarning("Customer not found: {CustomerId}", request.CustomerId);
                return new GetCustomerByIdResult
                {
                    Success = false,
                    ErrorMessage = $"Customer with ID {request.CustomerId} not found"
                };
            }
            
            var locationsQuery = @"
                SELECT
                    Id, CustomerId, LocationCode, LocationName, LocationType,
                    Address, City, District, Ward,
                    Latitude, Longitude, GeofenceRadiusMeters,
                    SiteManagerName, SiteManagerPhone,
                    OperatingHoursType, Requires24x7Coverage, MinimumGuardsRequired,
                    IsActive
                FROM customer_locations
                WHERE CustomerId = @CustomerId AND IsDeleted = 0
                ORDER BY LocationCode
            ";

            var locations = await connection.QueryAsync<CustomerLocation>(
                locationsQuery,
                new { request.CustomerId });
            
            var contractsQuery = @"
                SELECT
                    Id, CustomerId, DocumentId, ContractNumber, ContractTitle,
                    ContractType, ServiceScope, StartDate, EndDate, DurationMonths,
                    CoverageModel, FollowsCustomerCalendar, WorkOnPublicHolidays, WorkOnCustomerClosedDays,
                    IsRenewable, AutoRenewal, RenewalNoticeDays, RenewalCount,
                    AutoGenerateShifts, GenerateShiftsAdvanceDays,
                    Status, ApprovedBy, ApprovedAt, SignedDate, ActivatedAt,
                    TerminationDate, TerminationType, TerminationReason, TerminatedBy,
                    ContractFileUrl, Notes, CreatedAt
                FROM contracts
                WHERE CustomerId = @CustomerId AND IsDeleted = 0
                ORDER BY CreatedAt DESC
            ";

            var contracts = await connection.QueryAsync<Contract>(
                contractsQuery,
                new { request.CustomerId });

            var contractsList = contracts.ToList();
            
            var contractDtos = new List<ContractDto>();

            foreach (var contract in contractsList)
            {
                ContractDocumentDto? documentDto = null;
                if (contract.DocumentId.HasValue)
                {
                    var documentQuery = @"
                        SELECT
                            Id, DocumentType, Category, DocumentName, FileUrl, FileSize,
                            Version, StartDate, EndDate, SignDate, ApprovedAt, CreatedAt
                        FROM contract_documents
                        WHERE Id = @DocumentId AND IsDeleted = 0
                    ";

                    var document = await connection.QuerySingleOrDefaultAsync<ContractDocument>(
                        documentQuery,
                        new { DocumentId = contract.DocumentId.Value });

                    if (document != null)
                    {
                        documentDto = new ContractDocumentDto
                        {
                            Id = document.Id,
                            DocumentType = document.DocumentType,
                            Category = document.Category,
                            DocumentName = document.DocumentName,
                            FileUrl = document.FileUrl,
                            FileSize = document.FileSize,
                            Version = document.Version,
                            StartDate = document.StartDate,
                            EndDate = document.EndDate,
                            SignDate = document.SignDate,
                            ApprovedAt = document.ApprovedAt,
                            CreatedAt = document.CreatedAt
                        };
                    }
                }
                var contractLocationsQuery = @"
                    SELECT
                        Id, ContractId, LocationId, GuardsRequired, CoverageType,
                        ServiceStartDate, ServiceEndDate, IsPrimaryLocation,
                        PriorityLevel, AutoGenerateShifts, IsActive
                    FROM contract_locations
                    WHERE ContractId = @ContractId AND IsDeleted = 0
                    ORDER BY IsPrimaryLocation DESC, PriorityLevel
                ";

                var contractLocations = await connection.QueryAsync<ContractLocation>(
                    contractLocationsQuery,
                    new { ContractId = contract.Id });

                var contractLocationDtos = contractLocations.Select(cl => new ContractLocationDto
                {
                    Id = cl.Id,
                    ContractId = cl.ContractId,
                    LocationId = cl.LocationId,
                    GuardsRequired = cl.GuardsRequired,
                    CoverageType = cl.CoverageType,
                    ServiceStartDate = cl.ServiceStartDate,
                    ServiceEndDate = cl.ServiceEndDate,
                    IsPrimaryLocation = cl.IsPrimaryLocation,
                    PriorityLevel = cl.PriorityLevel,
                    AutoGenerateShifts = cl.AutoGenerateShifts,
                    IsActive = cl.IsActive
                }).ToList();
                
                var shiftSchedulesQuery = @"
                    SELECT
                        Id, ContractId, LocationId, ScheduleName, ScheduleType,
                        ShiftStartTime, ShiftEndTime, CrossesMidnight, DurationHours,
                        BreakMinutes, GuardsPerShift, RecurrenceType,
                        AppliesMonday, AppliesTuesday, AppliesWednesday, AppliesThursday,
                        AppliesFriday, AppliesSaturday, AppliesSunday, MonthlyDates,
                        AppliesOnPublicHolidays, AppliesOnCustomerHolidays, AppliesOnWeekends, SkipWhenLocationClosed,
                        RequiresArmedGuard, RequiresSupervisor, MinimumExperienceMonths, RequiredCertifications,
                        AutoGenerateEnabled, GenerateAdvanceDays,
                        EffectiveFrom, EffectiveTo, IsActive, Notes, CreatedBy
                    FROM contract_shift_schedules
                    WHERE ContractId = @ContractId AND IsDeleted = 0
                    ORDER BY ScheduleName
                ";

                var shiftSchedules = await connection.QueryAsync<ContractShiftSchedule>(
                    shiftSchedulesQuery,
                    new { ContractId = contract.Id });

                var shiftScheduleDtos = shiftSchedules.Select(ss => new ContractShiftScheduleDto
                {
                    Id = ss.Id,
                    ContractId = ss.ContractId,
                    LocationId = ss.LocationId,
                    ScheduleName = ss.ScheduleName,
                    ScheduleType = ss.ScheduleType,
                    ShiftStartTime = ss.ShiftStartTime,
                    ShiftEndTime = ss.ShiftEndTime,
                    CrossesMidnight = ss.CrossesMidnight,
                    DurationHours = ss.DurationHours,
                    BreakMinutes = ss.BreakMinutes,
                    GuardsPerShift = ss.GuardsPerShift,
                    RecurrenceType = ss.RecurrenceType,
                    AppliesMonday = ss.AppliesMonday,
                    AppliesTuesday = ss.AppliesTuesday,
                    AppliesWednesday = ss.AppliesWednesday,
                    AppliesThursday = ss.AppliesThursday,
                    AppliesFriday = ss.AppliesFriday,
                    AppliesSaturday = ss.AppliesSaturday,
                    AppliesSunday = ss.AppliesSunday,
                    MonthlyDates = ss.MonthlyDates,
                    AppliesOnPublicHolidays = ss.AppliesOnPublicHolidays,
                    AppliesOnCustomerHolidays = ss.AppliesOnCustomerHolidays,
                    AppliesOnWeekends = ss.AppliesOnWeekends,
                    SkipWhenLocationClosed = ss.SkipWhenLocationClosed,
                    RequiresArmedGuard = ss.RequiresArmedGuard,
                    RequiresSupervisor = ss.RequiresSupervisor,
                    MinimumExperienceMonths = ss.MinimumExperienceMonths,
                    RequiredCertifications = ss.RequiredCertifications,
                    AutoGenerateEnabled = ss.AutoGenerateEnabled,
                    GenerateAdvanceDays = ss.GenerateAdvanceDays,
                    EffectiveFrom = ss.EffectiveFrom,
                    EffectiveTo = ss.EffectiveTo,
                    IsActive = ss.IsActive,
                    Notes = ss.Notes,
                    CreatedBy = ss.CreatedBy
                }).ToList();
                
                var publicHolidaysQuery = @"
                    SELECT
                        Id, ContractId, HolidayDate, HolidayName, HolidayNameEn,
                        HolidayCategory, IsTetPeriod, IsTetHoliday, TetDayNumber,
                        HolidayStartDate, HolidayEndDate, TotalHolidayDays,
                        IsOfficialHoliday, IsObserved, OriginalDate, ObservedDate,
                        AppliesNationwide, AppliesToRegions,
                        StandardWorkplacesClosed, EssentialServicesOperating,
                        Description, Year
                    FROM public_holidays
                    WHERE (ContractId = @ContractId OR ContractId IS NULL)
                    AND Year >= YEAR(NOW())
                    ORDER BY HolidayDate
                ";

                var publicHolidays = await connection.QueryAsync<PublicHoliday>(
                    publicHolidaysQuery,
                    new { ContractId = contract.Id });

                var publicHolidayDtos = publicHolidays.Select(ph => new PublicHolidayDto
                {
                    Id = ph.Id,
                    ContractId = ph.ContractId,
                    HolidayDate = ph.HolidayDate,
                    HolidayName = ph.HolidayName,
                    HolidayNameEn = ph.HolidayNameEn,
                    HolidayCategory = ph.HolidayCategory,
                    IsTetPeriod = ph.IsTetPeriod,
                    IsTetHoliday = ph.IsTetHoliday,
                    TetDayNumber = ph.TetDayNumber,
                    HolidayStartDate = ph.HolidayStartDate,
                    HolidayEndDate = ph.HolidayEndDate,
                    TotalHolidayDays = ph.TotalHolidayDays,
                    IsOfficialHoliday = ph.IsOfficialHoliday,
                    IsObserved = ph.IsObserved,
                    OriginalDate = ph.OriginalDate,
                    ObservedDate = ph.ObservedDate,
                    AppliesNationwide = ph.AppliesNationwide,
                    AppliesToRegions = ph.AppliesToRegions,
                    StandardWorkplacesClosed = ph.StandardWorkplacesClosed,
                    EssentialServicesOperating = ph.EssentialServicesOperating,
                    Description = ph.Description,
                    Year = ph.Year
                }).ToList();

                contractDtos.Add(new ContractDto
                {
                    Id = contract.Id,
                    CustomerId = contract.CustomerId,
                    DocumentId = contract.DocumentId,
                    ContractNumber = contract.ContractNumber,
                    ContractTitle = contract.ContractTitle,
                    ContractType = contract.ContractType,
                    ServiceScope = contract.ServiceScope,
                    StartDate = contract.StartDate,
                    EndDate = contract.EndDate,
                    DurationMonths = contract.DurationMonths,
                    CoverageModel = contract.CoverageModel,
                    FollowsCustomerCalendar = contract.FollowsCustomerCalendar,
                    WorkOnPublicHolidays = contract.WorkOnPublicHolidays,
                    WorkOnCustomerClosedDays = contract.WorkOnCustomerClosedDays,
                    IsRenewable = contract.IsRenewable,
                    AutoRenewal = contract.AutoRenewal,
                    RenewalNoticeDays = contract.RenewalNoticeDays,
                    RenewalCount = contract.RenewalCount,
                    AutoGenerateShifts = contract.AutoGenerateShifts,
                    GenerateShiftsAdvanceDays = contract.GenerateShiftsAdvanceDays,
                    Status = contract.Status,
                    ApprovedBy = contract.ApprovedBy,
                    ApprovedAt = contract.ApprovedAt,
                    SignedDate = contract.SignedDate,
                    ActivatedAt = contract.ActivatedAt,
                    TerminationDate = contract.TerminationDate,
                    TerminationType = contract.TerminationType,
                    TerminationReason = contract.TerminationReason,
                    TerminatedBy = contract.TerminatedBy,
                    ContractFileUrl = contract.ContractFileUrl,
                    Notes = contract.Notes,
                    CreatedAt = contract.CreatedAt,
                    Document = documentDto,
                    Locations = contractLocationDtos,
                    ShiftSchedules = shiftScheduleDtos,
                    PublicHolidays = publicHolidayDtos
                });
            }

            var customerDto = new CustomerDetailDto
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
                AvatarUrl = customer.AvatarUrl,
                Gender = customer.Gender,
                DateOfBirth = customer.DateOfBirth,
                Address = customer.Address,
                City = customer.City,
                District = customer.District,
                Industry = customer.Industry,
                CompanySize = customer.CompanySize,
                Status = customer.Status,
                CustomerSince = customer.CustomerSince,
                FollowsNationalHolidays = customer.FollowsNationalHolidays,
                Notes = customer.Notes,
                CreatedAt = customer.CreatedAt
            };

            var locationDtos = locations.Select(l => new CustomerLocationDto
            {
                Id = l.Id,
                CustomerId = l.CustomerId,
                LocationCode = l.LocationCode,
                LocationName = l.LocationName,
                LocationType = l.LocationType,
                Address = l.Address,
                City = l.City,
                District = l.District,
                Ward = l.Ward,
                Latitude = l.Latitude,
                Longitude = l.Longitude,
                GeofenceRadiusMeters = l.GeofenceRadiusMeters,
                SiteManagerName = l.SiteManagerName,
                SiteManagerPhone = l.SiteManagerPhone,
                OperatingHoursType = l.OperatingHoursType,
                Requires24x7Coverage = l.Requires24x7Coverage,
                MinimumGuardsRequired = l.MinimumGuardsRequired,
                IsActive = l.IsActive
            }).ToList();

            logger.LogInformation(
                "Successfully retrieved customer {CustomerCode} with {LocationCount} locations and {ContractCount} contracts",
                customer.CustomerCode, locationDtos.Count, contractDtos.Count);

            return new GetCustomerByIdResult
            {
                Success = true,
                Customer = customerDto,
                Locations = locationDtos,
                Contracts = contractDtos
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting customer detail for ID: {CustomerId}", request.CustomerId);
            return new GetCustomerByIdResult
            {
                Success = false,
                ErrorMessage = $"Error getting customer detail: {ex.Message}"
            };
        }
    }
}
