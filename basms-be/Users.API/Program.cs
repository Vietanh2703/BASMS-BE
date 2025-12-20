using BuildingBlocks.Extensions;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddCarter();

builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

builder.Services.AddScoped<CreateUserValidator>();
builder.Services.AddScoped<DeleteUserValidator>();
builder.Services.AddScoped<UpdateUserValidator>();
builder.Services.AddScoped<CreateOtpValidator>();
builder.Services.AddScoped<VerifyOtpValidator>();
builder.Services.AddScoped<UpdateOtpValidator>();
builder.Services.AddScoped<UpdatePasswordValidator>();
builder.Services.AddScoped<ConfirmPasswordChangeValidator>();
builder.Services.AddScoped<RequestResetPasswordValidator>();
builder.Services.AddScoped<VerifyResetPasswordOtpValidator>();
builder.Services.AddScoped<CompleteResetPasswordValidator>();

builder.Services.AddSingleton<IDbConnectionFactory>(sp =>
{
    var connectionString = builder.Configuration["DB_CONNECTION_STRING_USERS"]
                        ?? builder.Configuration["ConnectionStrings__Database"]
                        ?? builder.Configuration.GetConnectionString("Database");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Database connection string is not configured properly");
    }

    Console.WriteLine($"Database Connection String: Server={ExtractServerFromConnectionString(connectionString)}");
    return new MySqlConnectionFactory(connectionString);
});

static string ExtractServerFromConnectionString(string connStr)
{
    try
    {
        var parts = connStr.Split(';');
        var serverPart = parts.FirstOrDefault(p => p.Trim().StartsWith("Server=", StringComparison.OrdinalIgnoreCase));
        return serverPart?.Split('=')[1] ?? "unknown";
    }
    catch { return "unknown"; }
}

try
{
    var credentialsPath = builder.Configuration["GOOGLE_APPLICATION_CREDENTIALS"];
    
    if (string.IsNullOrEmpty(credentialsPath))
    {
        credentialsPath = Path.Combine(Directory.GetCurrentDirectory(), "config", "firebase-credentials.json");
    }

    Console.WriteLine($"Loading Firebase credentials from: {credentialsPath}");

    if (File.Exists(credentialsPath))
    {
        var credential = GoogleCredential.FromFile(credentialsPath);
        
        FirebaseApp.Create(new AppOptions()
        {
            Credential = credential,
        });

        Console.WriteLine("Firebase initialized successfully");
    }
    else
    {
        throw new Exception($"Firebase credentials file not found at: {credentialsPath}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR initializing Firebase: {ex.Message}");
    throw;
}

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<Users.API.Consumers.CreateUserRequestConsumer>();
    x.AddConsumer<Users.API.Consumers.GetUserByEmailRequestConsumer>();
    
    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitMqHost = builder.Configuration["RABBITMQ_HOST"]
                        ?? builder.Configuration["RabbitMQ__Host"]
                        ?? builder.Configuration["RabbitMQ:Host"]
                        ?? "rabbitmq";

        var rabbitMqUsername = builder.Configuration["RABBITMQ_USERNAME"]
                            ?? builder.Configuration["RabbitMQ__Username"]
                            ?? builder.Configuration["RabbitMQ:Username"]
                            ?? "guest";

        var rabbitMqPassword = builder.Configuration["RABBITMQ_PASSWORD"]
                            ?? builder.Configuration["RabbitMQ__Password"]
                            ?? builder.Configuration["RabbitMQ:Password"]
                            ?? "guest";

        Console.WriteLine($"RabbitMQ Config - Host: {rabbitMqHost}, Username: {rabbitMqUsername}");
        
        if (string.IsNullOrWhiteSpace(rabbitMqHost))
        {
            throw new InvalidOperationException("RabbitMQ Host is not configured properly");
        }

        cfg.Host(rabbitMqHost, h =>
        {
            h.Username(rabbitMqUsername);
            h.Password(rabbitMqPassword);
        });
        
        cfg.ConfigureEndpoints(context);
    });
});


builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddScoped<EmailHandler>();

builder.Services.AddScoped<Users.API.Messaging.UserEventPublisher>();

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,              
        ValidateAudience = true,            
        ValidateLifetime = true,            
        ValidateIssuerSigningKey = true,   
        ValidIssuer = jwtSettings?.Issuer,
        ValidAudience = jwtSettings?.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.SecretKey ?? ""))
    };
});

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

var allowedOriginsString = builder.Configuration["ALLOWED_ORIGINS"]
                         ?? builder.Configuration["AllowedOrigins"]
                         ?? "";

var allowedOrigins = string.IsNullOrWhiteSpace(allowedOriginsString)
    ? new[] { "http://localhost:3000" }
    : allowedOriginsString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

Console.WriteLine($"CORS Allowed Origins: {string.Join(", ", allowedOrigins)}");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
});
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
    if (dbFactory is MySqlConnectionFactory mysqlFactory)
    {
        await mysqlFactory.EnsureTablesCreatedAsync();
        Console.WriteLine("Users database tables initialized successfully");
    }
}
app.UseGlobalExceptionHandler();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapCarter();
app.Run();