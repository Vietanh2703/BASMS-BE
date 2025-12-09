namespace Contracts.API.ContractsHandler.UpdateCustomer;

/// <summary>
/// Endpoint để update thông tin customer
/// </summary>
public class UpdateCustomerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: PUT /api/contracts/customers/{customerId}
        app.MapPut("/api/contracts/customers/{customerId}", async (
            Guid customerId,
            UpdateCustomerRequest request,
            ISender sender,
            ILogger<UpdateCustomerEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Update customer request for ID: {CustomerId}", customerId);

                // Map request to command
                var command = new UpdateCustomerCommand
                {
                    CustomerId = customerId,
                    CompanyName = request.CompanyName,
                    ContactPersonName = request.ContactPersonName,
                    ContactPersonTitle = request.ContactPersonTitle,
                    IdentityNumber = request.IdentityNumber,
                    IdentityIssueDate = request.IdentityIssueDate,
                    IdentityIssuePlace = request.IdentityIssuePlace,
                    DateOfBirth = request.DateOfBirth,
                    Address = request.Address
                };

                var result = await sender.Send(command);

                if (!result.Success)
                {
                    logger.LogError("Failed to update customer: {ErrorMessage}", result.ErrorMessage);
                    return Results.Problem(
                        title: "Error updating customer",
                        detail: result.ErrorMessage,
                        statusCode: StatusCodes.Status400BadRequest
                    );
                }

                logger.LogInformation(
                    "Successfully updated customer {CustomerCode} (ID: {CustomerId})",
                    result.CustomerCode, result.CustomerId);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing update customer request for ID: {CustomerId}", customerId);
                return Results.Problem(
                    title: "Error updating customer",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .RequireAuthorization()
        .WithTags("Contracts - Customers")
        .WithName("UpdateCustomer")
        .Produces<UpdateCustomerResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Update thông tin customer");
    }
}

/// <summary>
/// Request model cho UpdateCustomer endpoint
/// </summary>
public record UpdateCustomerRequest
{
    public string CompanyName { get; init; } = string.Empty;
    public string ContactPersonName { get; init; } = string.Empty;
    public string? ContactPersonTitle { get; init; }
    public string IdentityNumber { get; init; } = string.Empty;
    public DateTime? IdentityIssueDate { get; init; }
    public string? IdentityIssuePlace { get; init; }
    public DateTime DateOfBirth { get; init; }
    public string Address { get; init; } = string.Empty;
}
