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
                        // Time
                        startTime = t.StartTime.ToString(@"hh\:mm"),
                        endTime = t.EndTime.ToString(@"hh\:mm"),
                        durationHours = t.DurationHours,
                        breakDurationMinutes = t.BreakDurationMinutes,
                        // Classification
                        isNightShift = t.IsNightShift,
                        crossesMidnight = t.CrossesMidnight,
                        // Days of week
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
                        // Staffing
                        minGuardsRequired = t.MinGuardsRequired,
                        maxGuardsAllowed = t.MaxGuardsAllowed,
                        optimalGuards = t.OptimalGuards,
                        // Location
                        locationId = t.LocationId,
                        locationName = t.LocationName,
                        locationAddress = t.LocationAddress,
                        // Status
                        status = t.Status,
                        isActive = t.IsActive,
                        effectiveFrom = t.EffectiveFrom,
                        effectiveTo = t.EffectiveTo,
                        // Audit
                        createdAt = t.CreatedAt,
                        updatedAt = t.UpdatedAt
                    })
                })
            });
        })
        .RequireAuthorization()
        .WithTags("Shift Templates")
        .WithName("GetAllShiftTemplateRequest")
        .Produces<GetAllShiftTemplateRequestResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get all shift templates pending shift creation")
        .WithDescription(@"
            Retrieves all shift templates with status 'await_create_shift' for a manager,
            grouped by ContractId.

            **Features:**
            - Filters templates by status = 'await_create_shift'
            - Groups templates by ContractId
            - Each contract shows total templates and locations
            - Calculates summary statistics per contract

            **Use Case:**
            This endpoint is used when a manager wants to see all pending shift templates
            that need to be converted into actual shifts. Each contract group shows:
            - How many templates are waiting
            - Which locations are involved
            - Total guards required across all templates

            **Response Structure:**
            ```json
            {
              ""success"": true,
              ""managerId"": ""660e8400-e29b-41d4-a716-446655440000"",
              ""totalContracts"": 3,
              ""totalTemplates"": 15,
              ""contractGroups"": [
                {
                  ""contractId"": ""770e8400-e29b-41d4-a716-446655440000"",
                  ""contractName"": ""Contract 770e8400"",
                  ""templateCount"": 8,
                  ""totalLocations"": 2,
                  ""locationNames"": [""Vincom Center"", ""Landmark 81""],
                  ""totalMinGuardsRequired"": 24,
                  ""templates"": [
                    {
                      ""id"": ""880e8400-e29b-41d4-a716-446655440000"",
                      ""templateCode"": ""MORNING-8H"",
                      ""templateName"": ""Ca Sáng 08:00-17:00"",
                      ""startTime"": ""08:00"",
                      ""endTime"": ""17:00"",
                      ""durationHours"": 8.0,
                      ""minGuardsRequired"": 3,
                      ""locationName"": ""Vincom Center"",
                      ""daysOfWeek"": {
                        ""monday"": true,
                        ""tuesday"": true,
                        ""wednesday"": true,
                        ""thursday"": true,
                        ""friday"": true,
                        ""saturday"": false,
                        ""sunday"": false
                      },
                      ""status"": ""await_create_shift"",
                      ""createdAt"": ""2024-01-15T10:30:00Z""
                    }
                  ]
                }
              ]
            }
            ```

            **Next Steps:**
            After reviewing the pending templates, the manager can use the
            GenerateShifts or BulkGenerateAndAssignShifts endpoints to create
            actual shifts from these templates.
        ");
    }
}
