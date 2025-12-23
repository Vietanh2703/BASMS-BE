namespace Shifts.API.ShiftsHandler.GetUnassignedShiftGroups;

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
                "Getting unassigned shift groups for Manager {ManagerId}, ContractId={ContractId}",
                request.ManagerId,
                request.ContractId?.ToString() ?? "ALL");

            using var connection = await dbFactory.CreateConnectionAsync();
            var contractFilter = request.ContractId.HasValue
                ? "AND s.ContractId = @ContractId"
                : "";

            var sql = $@"
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
                        s.AssignedGuardsCount,
                        s.ShiftDate,
                        s.IsNightShift,
                        s.ShiftType,
                        s.Status,
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
                        AND s.Status IN ('DRAFT', 'SCHEDULED', 'PARTIAL')
                        AND s.IsDeleted = 0
                        AND st.ManagerId = @ManagerId
                        AND st.IsDeleted = 0
                        AND NOT EXISTS (
                            SELECT 1
                            FROM shift_assignments sa
                            WHERE sa.ShiftId = s.Id
                              AND sa.IsDeleted = 0
                        )
                        {contractFilter}
                ),
                GroupStats AS (
                    SELECT
                        s.ShiftTemplateId,
                        s.ContractId,
                        COUNT(*) as UnassignedCount,
                        MIN(s.ShiftDate) as NearestDate,
                        MAX(s.ShiftDate) as FarthestDate
                    FROM shifts s
                    INNER JOIN shift_templates st ON s.ShiftTemplateId = st.Id
                    WHERE
                        s.AssignedGuardsCount = 0
                        AND s.Status IN ('DRAFT', 'SCHEDULED', 'PARTIAL')
                        AND s.IsDeleted = 0
                        AND st.ManagerId = @ManagerId
                        AND st.IsDeleted = 0
                        AND NOT EXISTS (
                            SELECT 1
                            FROM shift_assignments sa
                            WHERE sa.ShiftId = s.Id
                              AND sa.IsDeleted = 0
                        )
                        {contractFilter}
                    GROUP BY s.ShiftTemplateId, s.ContractId
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

            if (request.ContractId.HasValue)
            {
                parameters.Add("ContractId", request.ContractId.Value);
            }

            var groups = await connection.QueryAsync<UnassignedShiftGroupDto>(sql, parameters);
            var groupsList = groups.ToList();

            logger.LogInformation(
                "Found {Count} unassigned shift groups for Manager {ManagerId}",
                groupsList.Count,
                request.ManagerId);
            
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
