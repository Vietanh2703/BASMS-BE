using Dapper;

namespace Shifts.API.ShiftsHandler.GetUnassignedShiftGroups;

/// <summary>
/// Handler để lấy danh sách ca trực chưa được phân công, nhóm theo TemplateId và ContractId
/// Chỉ hiện 1 đại diện cho mỗi nhóm
/// </summary>
internal class GetUnassignedShiftGroupsHandler(
    IDbConnectionFactory dbFactory,
    ILogger<GetUnassignedShiftGroupsHandler> logger)
    : IQueryHandler<GetUnassignedShiftGroupsQuery, GetUnassignedShiftGroupsResult>
{
    public async Task<GetUnassignedShiftGroupsResult> Handle(
        GetUnassignedShiftGroupsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Getting unassigned shift groups for Manager {ManagerId}",
                request.ManagerId);

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // SQL QUERY - GROUP BY TemplateId và ContractId
            // ================================================================
            // Logic:
            // 1. Join shifts với shift_templates để lấy thông tin template
            // 2. Filter: AssignedGuardsCount = 0 (chưa phân công)
            // 3. Filter: ManagerId từ shift_templates
            // 4. Group by ShiftTemplateId, ContractId
            // 5. Lấy thông tin từ một shift đại diện (shift gần nhất)
            // ================================================================

            var sql = @"
                WITH UnassignedShifts AS (
                    SELECT
                        s.Id,
                        s.ShiftTemplateId,
                        s.ContractId,
                        s.LocationId,
                        s.LocationName,
                        s.LocationAddress,
                        s.ShiftStart,
                        s.ShiftEnd,
                        s.WorkDurationHours,
                        s.RequiredGuards,
                        s.ShiftDate,
                        s.IsNightShift,
                        s.ShiftType,
                        st.TemplateName,
                        st.TemplateCode,
                        ROW_NUMBER() OVER (
                            PARTITION BY s.ShiftTemplateId, s.ContractId
                            ORDER BY s.ShiftDate ASC
                        ) as RowNum
                    FROM shifts s
                    INNER JOIN shift_templates st ON s.ShiftTemplateId = st.Id
                    WHERE
                        s.AssignedGuardsCount = 0
                        AND s.IsDeleted = 0
                        AND st.ManagerId = @ManagerId
                        AND st.IsDeleted = 0
                ),
                GroupStats AS (
                    SELECT
                        ShiftTemplateId,
                        ContractId,
                        COUNT(*) as UnassignedCount,
                        MIN(ShiftDate) as NearestDate,
                        MAX(ShiftDate) as FarthestDate
                    FROM shifts s
                    INNER JOIN shift_templates st ON s.ShiftTemplateId = st.Id
                    WHERE
                        s.AssignedGuardsCount = 0
                        AND s.IsDeleted = 0
                        AND st.ManagerId = @ManagerId
                        AND st.IsDeleted = 0
                    GROUP BY ShiftTemplateId, ContractId
                )
                SELECT
                    us.Id as RepresentativeShiftId,
                    us.ShiftTemplateId,
                    us.ContractId,
                    us.TemplateName,
                    us.TemplateCode,
                    us.LocationId,
                    us.LocationName,
                    us.LocationAddress,
                    us.ShiftStart,
                    us.ShiftEnd,
                    us.WorkDurationHours,
                    us.RequiredGuards,
                    us.IsNightShift,
                    us.ShiftType,
                    gs.UnassignedCount as UnassignedShiftCount,
                    gs.NearestDate as NearestShiftDate,
                    gs.FarthestDate as FarthestShiftDate
                FROM UnassignedShifts us
                INNER JOIN GroupStats gs
                    ON us.ShiftTemplateId = gs.ShiftTemplateId
                    AND COALESCE(us.ContractId, '00000000-0000-0000-0000-000000000000') = COALESCE(gs.ContractId, '00000000-0000-0000-0000-000000000000')
                WHERE us.RowNum = 1
                ORDER BY
                    us.ShiftDate ASC,
                    us.ShiftStart ASC";

            var parameters = new DynamicParameters();
            parameters.Add("ManagerId", request.ManagerId);

            var groups = await connection.QueryAsync<UnassignedShiftGroupDto>(sql, parameters);
            var groupsList = groups.ToList();

            logger.LogInformation(
                "Found {Count} unassigned shift groups for Manager {ManagerId}",
                groupsList.Count,
                request.ManagerId);

            // Calculate total unassigned shifts
            var totalUnassigned = groupsList.Sum(g => g.UnassignedShiftCount);

            logger.LogInformation(
                "Total unassigned shifts: {TotalUnassigned} across {GroupCount} groups",
                totalUnassigned,
                groupsList.Count);

            return new GetUnassignedShiftGroupsResult
            {
                Success = true,
                ShiftGroups = groupsList,
                TotalGroups = groupsList.Count
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error getting unassigned shift groups for Manager {ManagerId}",
                request.ManagerId);

            return new GetUnassignedShiftGroupsResult
            {
                Success = false,
                ShiftGroups = new List<UnassignedShiftGroupDto>(),
                TotalGroups = 0,
                ErrorMessage = $"Failed to get unassigned shift groups: {ex.Message}"
            };
        }
    }
}
