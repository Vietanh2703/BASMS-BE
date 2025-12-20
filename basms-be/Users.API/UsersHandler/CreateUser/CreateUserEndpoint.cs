namespace Users.API.UsersHandler.CreateUser;

public record CreateUserRequest(
    string IdentityNumber,
    DateTime IdentityIssueDate,
    string IdentityIssuePlace,
    string Email,              
    string Password,           
    string FullName,           
    string? Phone,             
    string Gender,
    string? Address,           
    int? BirthDay,            
    int? BirthMonth,          
    int? BirthYear,          
    Guid? RoleId,             
    string? AvatarUrl,        
    string AuthProvider = "email"  
);


public record CreateUserResponse(
    Guid Id,                  
    string FirebaseUid,      
    string Email             
);

public class CreateUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users/create", async (CreateUserRequest request, ISender sender, ILogger<CreateUserEndpoint> logger, HttpContext httpContext) =>
        {
            logger.LogInformation("CreateUserEndpoint HIT - Method: {Method}, Path: {Path}, ContentType: {ContentType}",
                httpContext.Request.Method, httpContext.Request.Path, httpContext.Request.ContentType);
            
            var command = request.Adapt<CreateUserCommand>();
            
            var result = await sender.Send(command);
            
            var response = result.Adapt<CreateUserResponse>();
            
            return Results.Created($"/users/{response.Id}", response);
        })
        .RequireAuthorization()
        .WithTags("Users")  
        .WithName("CreateUser")  
        .Produces<CreateUserResponse>(StatusCodes.Status201Created)  
        .ProducesProblem(StatusCodes.Status400BadRequest) 
        .ProducesProblem(StatusCodes.Status401Unauthorized) 
        .ProducesProblem(StatusCodes.Status403Forbidden) 
        .ProducesProblem(StatusCodes.Status409Conflict) 
        .ProducesProblem(StatusCodes.Status500InternalServerError) 
        .WithSummary("Creates a new user");
    }
}