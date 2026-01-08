using Shifts.API.Utilities;

namespace Shifts.API.ShiftsHandler.GetAllShiftTemplateRequest;

public class GetAllShiftTemplateRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/shift-templates/pending/{managerId}", async (
            Guid managerId,
            ISender sender) =>
        {
            var query = new GetAllShiftTemplateRequestQuery(managerId);
            var result = await sender.Send(query);

            if (!result.Success)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    message = result.ErrorMessage,
                    managerId = result.ManagerId
                });
            }

            return Results.Ok(new
            {
                success = true,
                managerId = result.ManagerId,
                totalContracts = result.TotalContracts,
                totalTemplates = result.TotalTemplates,
                contractGroups = result.ContractGroups.Select(contract => new
                {
                    contractId = contract.ContractId,
                    contractName = contract.ContractName,
                    templateCount = contract.TemplateCount,
                    totalLocations = contract.TotalLocations,
                    locationNames = contract.LocationNames,
                    totalMinGuardsRequired = contract.TotalMinGuardsRequired,
                    templates = contract.Templates.Select(t => new
                    {
                        id = t.Id,
                        managerId = t.ManagerId,
                        contractId = t.ContractId,
                        templateCode = t.TemplateCode,
                        templateName = t.TemplateName,
                        description = t.Description,
                        startTime = t.StartTime.ToString(@"hh\:mm"),
                        endTime = t.EndTime.ToString(@"hh\:mm"),
                        durationHours = t.DurationHours,
                        breakDurationMinutes = t.BreakDurationMinutes,
                        isNightShift = t.IsNightShift,
                        crossesMidnight = t.CrossesMidnight,
                        daysOfWeek = new
                        {
                            monday = t.AppliesMonday,
                            tuesday = t.AppliesTuesday,
                            wednesday = t.AppliesWednesday,
                            thursday = t.AppliesThursday,
                            friday = t.AppliesFriday,
                            saturday = t.AppliesSaturday,
                            sunday = t.AppliesSunday
                        },
                        minGuardsRequired = t.MinGuardsRequired,
                        maxGuardsAllowed = t.MaxGuardsAllowed,
                        optimalGuards = t.OptimalGuards,
                        locationId = t.LocationId,
                        locationName = t.LocationName,
                        locationAddress = t.LocationAddress,
                        status = t.Status,
                        isActive = t.IsActive,
                        effectiveFrom = t.EffectiveFrom,
                        effectiveTo = t.EffectiveTo,
                        createdAt = t.CreatedAt,
                        updatedAt = t.UpdatedAt
                    })
                })
            });
        })
        .AddStandardGetDocumentation<GetAllShiftTemplateRequestResult>(
            tag: "Shift Templates",
            name: "GetAllShiftTemplateRequest",
            summary: "Get all shift templates pending shift creation");
    }
}
