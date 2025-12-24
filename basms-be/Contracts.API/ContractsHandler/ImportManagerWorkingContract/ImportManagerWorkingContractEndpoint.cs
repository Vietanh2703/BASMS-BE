namespace Contracts.API.ContractsHandler.ImportManagerWorkingContract;

public class ImportManagerWorkingContractEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: POST /api/contracts/manager-working/import
        app.MapPost("/api/contracts/manager-working/import",
                async (HttpRequest request, ISender sender, ILogger<ImportManagerWorkingContractEndpoint> logger) =>
                {
                    try
                    {
                        Guid documentId;

                        // Hỗ trợ cả JSON và Form-Data
                        if (request.HasFormContentType)
                        {
                            var documentIdStr = request.Form["documentId"].ToString();
                            if (!Guid.TryParse(documentIdStr, out documentId))
                                return Results.BadRequest(new { success = false, error = "Invalid documentId format" });
                        }
                        else if (request.ContentType?.Contains("application/json") == true)
                        {
                            var req = await request.ReadFromJsonAsync<ImportManagerWorkingContractRequest>();
                            if (req == null)
                                return Results.BadRequest(new { success = false, error = "Invalid request body" });
                            documentId = req.DocumentId;
                        }
                        else
                        {
                            return Results.BadRequest(new
                            {
                                success = false,
                                error = "Unsupported content type. Use application/json or multipart/form-data"
                            });
                        }

                        logger.LogInformation("Importing manager working contract from DocumentId: {DocumentId}", documentId);

                        var command = new ImportManagerWorkingContractCommand(documentId);
                        var result = await sender.Send(command);

                        if (!result.Success)
                            return Results.BadRequest(new { success = false, error = result.ErrorMessage });

                        return Results.Ok(new
                        {
                            success = true,
                            data = new
                            {
                                contractId = result.ContractId,
                                userId = result.UserId,
                                contractNumber = result.ContractNumber,
                                contractTitle = result.ContractTitle,
                                employeeName = result.EmployeeName,
                                employeeEmail = result.EmployeeEmail,
                                contractType = "manager_working_contract",
                                status = "signed",
                                message = "Hợp đồng lao động quản lý đã được import thành công."
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error in import manager working contract endpoint");
                        return Results.Problem(
                            title: "Import failed",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status500InternalServerError
                        );
                    }
                })
            .RequireAuthorization()
            .WithTags("Contracts")
            .WithName("ImportManagerWorkingContract")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Import hợp đồng lao động quản lý từ Word document đã ký");
    }
}


public record ImportManagerWorkingContractRequest(
    Guid DocumentId
);
