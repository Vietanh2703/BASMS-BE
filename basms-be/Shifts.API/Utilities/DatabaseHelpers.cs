using BuildingBlocks.Exceptions;
namespace Shifts.API.Utilities;

public static class DatabaseHelpers
{
    #region Guard Helpers
    public static async Task<Guards> GetGuardByIdOrThrowAsync(
        this IDbConnection connection,
        Guid guardId,
        IDbTransaction? transaction = null,
        string? customErrorMessage = null)
    {
        var guards = await connection.GetAllAsync<Guards>(transaction);
        var guard = guards.FirstOrDefault(g => g.Id == guardId && !g.IsDeleted);

        if (guard == null)
        {
            throw new NotFoundException(
                customErrorMessage ?? $"Guard with ID {guardId} not found",
                "GUARD_NOT_FOUND");
        }

        return guard;
    }

    /// <summary>
    /// Get guard by Email với validation
    /// </summary>
    public static async Task<Guards> GetGuardByEmailOrThrowAsync(
        this IDbConnection connection,
        string email,
        IDbTransaction? transaction = null,
        string? customErrorMessage = null)
    {
        var guards = await connection.GetAllAsync<Guards>(transaction);
        var guard = guards.FirstOrDefault(g =>
            !string.IsNullOrEmpty(g.Email) &&
            g.Email.Equals(email, StringComparison.OrdinalIgnoreCase) &&
            !g.IsDeleted);

        if (guard == null)
        {
            throw new NotFoundException(
                customErrorMessage ?? $"Guard with email {email} not found",
                "GUARD_NOT_FOUND");
        }

        return guard;
    }

    /// <summary>
    /// Get guard by Employee Code
    /// </summary>
    public static async Task<Guards?> GetGuardByEmployeeCodeAsync(
        this IDbConnection connection,
        string employeeCode,
        IDbTransaction? transaction = null)
    {
        var guards = await connection.GetAllAsync<Guards>(transaction);
        return guards.FirstOrDefault(g => g.EmployeeCode == employeeCode && !g.IsDeleted);
    }

    /// <summary>
    /// Check xem guard có tồn tại không
    /// </summary>
    public static async Task<bool> IsGuardExistsAsync(
        this IDbConnection connection,
        Guid guardId,
        IDbTransaction? transaction = null)
    {
        var guards = await connection.GetAllAsync<Guards>(transaction);
        return guards.Any(g => g.Id == guardId && !g.IsDeleted);
    }

    /// <summary>
    /// Get guards by IDs
    /// </summary>
    public static async Task<IEnumerable<Guards>> GetGuardsByIdsAsync(
        this IDbConnection connection,
        IEnumerable<Guid> guardIds,
        IDbTransaction? transaction = null,
        bool includeDeleted = false)
    {
        var guards = await connection.GetAllAsync<Guards>(transaction);
        var guardIdList = guardIds.ToList();

        return guards.Where(g =>
            guardIdList.Contains(g.Id) &&
            (includeDeleted || !g.IsDeleted));
    }

    /// <summary>
    /// Soft delete guard
    /// </summary>
    public static async Task SoftDeleteGuardAsync(
        this IDbConnection connection,
        Guards guard,
        IDbTransaction transaction)
    {
        guard.IsDeleted = true;
        guard.IsActive = false;
        guard.UpdatedAt = DateTime.UtcNow;
        await connection.UpdateAsync(guard, transaction);
    }

    #endregion

    #region Manager Helpers
    public static async Task<Managers> GetManagerByIdOrThrowAsync(
        this IDbConnection connection,
        Guid managerId,
        IDbTransaction? transaction = null,
        string? customErrorMessage = null)
    {
        var managers = await connection.GetAllAsync<Managers>(transaction);
        var manager = managers.FirstOrDefault(m => m.Id == managerId && !m.IsDeleted);

        if (manager == null)
        {
            throw new NotFoundException(
                customErrorMessage ?? $"Manager with ID {managerId} not found",
                "MANAGER_NOT_FOUND");
        }

        return manager;
    }

    /// <summary>
    /// Get manager by Email với validation
    /// </summary>
    public static async Task<Managers> GetManagerByEmailOrThrowAsync(
        this IDbConnection connection,
        string email,
        IDbTransaction? transaction = null,
        string? customErrorMessage = null)
    {
        var managers = await connection.GetAllAsync<Managers>(transaction);
        var manager = managers.FirstOrDefault(m =>
            m.Email.Equals(email, StringComparison.OrdinalIgnoreCase) &&
            !m.IsDeleted);

        if (manager == null)
        {
            throw new NotFoundException(
                customErrorMessage ?? $"Manager with email {email} not found",
                "MANAGER_NOT_FOUND");
        }

        return manager;
    }

    /// <summary>
    /// Get manager by Employee Code
    /// </summary>
    public static async Task<Managers?> GetManagerByEmployeeCodeAsync(
        this IDbConnection connection,
        string employeeCode,
        IDbTransaction? transaction = null)
    {
        var managers = await connection.GetAllAsync<Managers>(transaction);
        return managers.FirstOrDefault(m => m.EmployeeCode == employeeCode && !m.IsDeleted);
    }

    /// <summary>
    /// Check xem manager có tồn tại không
    /// </summary>
    public static async Task<bool> IsManagerExistsAsync(
        this IDbConnection connection,
        Guid managerId,
        IDbTransaction? transaction = null)
    {
        var managers = await connection.GetAllAsync<Managers>(transaction);
        return managers.Any(m => m.Id == managerId && !m.IsDeleted);
    }

    /// <summary>
    /// Soft delete manager
    /// </summary>
    public static async Task SoftDeleteManagerAsync(
        this IDbConnection connection,
        Managers manager,
        IDbTransaction transaction)
    {
        manager.IsDeleted = true;
        manager.IsActive = false;
        manager.UpdatedAt = DateTime.UtcNow;
        await connection.UpdateAsync(manager, transaction);
    }

    #endregion

    #region Shift Helpers
    public static async Task<Models.Shifts> GetShiftByIdOrThrowAsync(
        this IDbConnection connection,
        Guid shiftId,
        IDbTransaction? transaction = null,
        string? customErrorMessage = null)
    {
        var shift = await connection.GetAsync<Models.Shifts>(shiftId, transaction);

        if (shift == null || shift.IsDeleted)
        {
            throw new NotFoundException(
                customErrorMessage ?? $"Shift with ID {shiftId} not found",
                "SHIFT_NOT_FOUND");
        }

        return shift;
    }

    /// <summary>
    /// Get shifts by date range
    /// </summary>
    public static async Task<IEnumerable<Models.Shifts>> GetShiftsByDateRangeAsync(
        this IDbConnection connection,
        DateTime startDate,
        DateTime endDate,
        IDbTransaction? transaction = null,
        bool includeDeleted = false)
    {
        var shifts = await connection.GetAllAsync<Models.Shifts>(transaction);

        return shifts.Where(s =>
            s.ShiftDate >= startDate &&
            s.ShiftDate <= endDate &&
            (includeDeleted || !s.IsDeleted));
    }

    /// <summary>
    /// Check if shift exists for location at specific time
    /// </summary>
    public static async Task<bool> IsShiftExistsAtTimeAsync(
        this IDbConnection connection,
        Guid locationId,
        DateTime shiftDate,
        TimeSpan startTime,
        TimeSpan endTime,
        Guid? excludeShiftId = null,
        IDbTransaction? transaction = null)
    {
        var shifts = await connection.GetAllAsync<Models.Shifts>(transaction);

        return shifts.Any(s =>
            s.LocationId == locationId &&
            s.ShiftDate.Date == shiftDate.Date &&
            !s.IsDeleted &&
            (excludeShiftId == null || s.Id != excludeShiftId.Value) &&
            ((s.ShiftStart.TimeOfDay < endTime && s.ShiftEnd.TimeOfDay > startTime)));
    }

    /// <summary>
    /// Soft delete shift
    /// </summary>
    public static async Task SoftDeleteShiftAsync(
        this IDbConnection connection,
        Models.Shifts shift,
        IDbTransaction transaction)
    {
        shift.IsDeleted = true;
        shift.Status = "CANCELLED";
        shift.UpdatedAt = DateTime.UtcNow;
        await connection.UpdateAsync(shift, transaction);
    }

    #endregion

    #region Team Helpers
    public static async Task<Teams> GetTeamByIdOrThrowAsync(
        this IDbConnection connection,
        Guid teamId,
        IDbTransaction? transaction = null,
        string? customErrorMessage = null)
    {
        var team = await connection.GetAsync<Teams>(teamId, transaction);

        if (team == null || team.IsDeleted)
        {
            throw new NotFoundException(
                customErrorMessage ?? $"Team with ID {teamId} not found",
                "TEAM_NOT_FOUND");
        }

        return team;
    }

    /// <summary>
    /// Get teams by manager ID
    /// </summary>
    public static async Task<IEnumerable<Teams>> GetTeamsByManagerIdAsync(
        this IDbConnection connection,
        Guid managerId,
        IDbTransaction? transaction = null,
        bool includeDeleted = false)
    {
        var teams = await connection.GetAllAsync<Teams>(transaction);

        return teams.Where(t =>
            t.ManagerId == managerId &&
            (includeDeleted || !t.IsDeleted));
    }

    /// <summary>
    /// Soft delete team
    /// </summary>
    public static async Task SoftDeleteTeamAsync(
        this IDbConnection connection,
        Teams team,
        IDbTransaction transaction)
    {
        team.IsDeleted = true;
        team.IsActive = false;
        team.UpdatedAt = DateTime.UtcNow;
        await connection.UpdateAsync(team, transaction);
    }

    #endregion

    #region Shift Assignment Helpers
    public static async Task<ShiftAssignments> GetShiftAssignmentByIdOrThrowAsync(
        this IDbConnection connection,
        Guid assignmentId,
        IDbTransaction? transaction = null,
        string? customErrorMessage = null)
    {
        var assignment = await connection.GetAsync<ShiftAssignments>(assignmentId, transaction);

        if (assignment == null || assignment.IsDeleted)
        {
            throw new NotFoundException(
                customErrorMessage ?? $"Shift assignment with ID {assignmentId} not found",
                "ASSIGNMENT_NOT_FOUND");
        }

        return assignment;
    }

    /// <summary>
    /// Get shift assignments by shift ID
    /// </summary>
    public static async Task<IEnumerable<ShiftAssignments>> GetAssignmentsByShiftIdAsync(
        this IDbConnection connection,
        Guid shiftId,
        IDbTransaction? transaction = null,
        bool includeDeleted = false)
    {
        var assignments = await connection.GetAllAsync<ShiftAssignments>(transaction);

        return assignments.Where(a =>
            a.ShiftId == shiftId &&
            (includeDeleted || !a.IsDeleted));
    }

    /// <summary>
    /// Get shift assignments by guard ID
    /// </summary>
    public static async Task<IEnumerable<ShiftAssignments>> GetAssignmentsByGuardIdAsync(
        this IDbConnection connection,
        Guid guardId,
        IDbTransaction? transaction = null,
        bool includeDeleted = false)
    {
        var assignments = await connection.GetAllAsync<ShiftAssignments>(transaction);

        return assignments.Where(a =>
            a.GuardId == guardId &&
            (includeDeleted || !a.IsDeleted));
    }

    #endregion

    #region Wage Rate Helpers
    public static async Task<WageRates> GetWageRateByIdOrThrowAsync(
        this IDbConnection connection,
        Guid wageRateId,
        IDbTransaction? transaction = null,
        string? customErrorMessage = null)
    {
        var wageRate = await connection.GetAsync<WageRates>(wageRateId, transaction);

        if (wageRate == null || !wageRate.IsActive)
        {
            throw new NotFoundException(
                customErrorMessage ?? $"Wage rate with ID {wageRateId} not found",
                "WAGE_RATE_NOT_FOUND");
        }

        return wageRate;
    }

    /// <summary>
    /// Get active wage rates
    /// </summary>
    public static async Task<IEnumerable<WageRates>> GetActiveWageRatesAsync(
        this IDbConnection connection,
        IDbTransaction? transaction = null)
    {
        var wageRates = await connection.GetAllAsync<WageRates>(transaction);
        var currentDate = DateTime.UtcNow;

        return wageRates.Where(w =>
            w.IsActive &&
            w.EffectiveFrom <= currentDate &&
            (w.EffectiveTo == null || w.EffectiveTo >= currentDate));
    }

    #endregion
}
