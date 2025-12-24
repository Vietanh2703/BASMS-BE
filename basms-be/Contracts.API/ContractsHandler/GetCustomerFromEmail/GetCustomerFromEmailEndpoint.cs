namespace Contracts.API.ContractsHandler.GetCustomerFromEmail;

public class GetCustomerFromEmailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contracts/customers/by-email", async (string email, ISender sender) =>
        {
            var query = new GetCustomerFromEmailQuery(email);
            var result = await sender.Send(query);

            if (!result.Success)
            {
                return Results.NotFound(new
                {
                    success = false,
                    message = result.ErrorMessage,
                    email = email
                });
            }

            return Results.Ok(new
            {
                success = true,
                customer = new
                {
                    id = result.Customer!.Id,
                    customerCode = result.Customer.CustomerCode,
                    companyName = result.Customer.CompanyName,
                    contactPersonName = result.Customer.ContactPersonName,
                    contactPersonTitle = result.Customer.ContactPersonTitle,
                    identityNumber = result.Customer.IdentityNumber,
                    identityIssueDate = result.Customer.IdentityIssueDate,
                    identityIssuePlace = result.Customer.IdentityIssuePlace,
                    email = result.Customer.Email,
                    phone = result.Customer.Phone,
                    avatarUrl = result.Customer.AvatarUrl,
                    gender = result.Customer.Gender,
                    dateOfBirth = result.Customer.DateOfBirth,
                    address = result.Customer.Address,
                    city = result.Customer.City,
                    district = result.Customer.District,
                    industry = result.Customer.Industry,
                    companySize = result.Customer.CompanySize,
                    status = result.Customer.Status,
                    customerSince = result.Customer.CustomerSince,
                    followsNationalHolidays = result.Customer.FollowsNationalHolidays,
                    notes = result.Customer.Notes,
                    createdAt = result.Customer.CreatedAt
                }
            });
        })
        .RequireAuthorization()
        .WithTags("Contracts")
        .WithName("GetCustomerFromEmail")
        .Produces<GetCustomerFromEmailResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get customer by email");
    }
}
