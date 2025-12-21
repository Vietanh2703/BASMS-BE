var builder = WebApplication.CreateBuilder(args);

// Đăng ký Carter
builder.Services.AddCarter();

// Đăng ký MediatR
builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

// Đăng ký SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true; // Enable for development
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// Đăng ký SignalR-related services
builder.Services.AddSingleton<IUserConnectionManager, UserConnectionManager>();
builder.Services.AddScoped<IPresenceService, PresenceService>();

// Đăng ký Dapper connection factory
builder.Services.AddSingleton<IDbConnectionFactory>(sp =>
{
    var connectionString = builder.Configuration["DB_CONNECTION_STRING_CHATS"]
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

// Đăng ký AWS S3 Client
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonS3>();

// Đăng ký MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitMqHost = builder.Configuration["RABBITMQ_HOST"] ?? builder.Configuration["RabbitMQ__Host"] ?? "localhost";
        var rabbitMqUsername = builder.Configuration["RABBITMQ_USERNAME"] ?? builder.Configuration["RabbitMQ__Username"] ?? "guest";
        var rabbitMqPassword = builder.Configuration["RABBITMQ_PASSWORD"] ?? builder.Configuration["RabbitMQ__Password"] ?? "guest";

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
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

// CORS
var allowedOrigins = (builder.Configuration["ALLOWED_ORIGINS"] ?? builder.Configuration["AllowedOrigins"] ?? "http://localhost:3000,http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

Console.WriteLine($"CORS Allowed Origins: {string.Join(", ", allowedOrigins)}");

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Initialize database tables
using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
    if (dbFactory is MySqlConnectionFactory mysqlFactory)
    {
        await mysqlFactory.EnsureTablesCreatedAsync();
        Console.WriteLine("✓ Chats database tables initialized successfully");
    }
}

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseGlobalExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapCarter();
app.MapHub<ChatHub>("/chathub");
app.MapGet("/", () => "Chats.API is running.");

Console.WriteLine("✓ SignalR ChatHub mapped at /chathub");

app.Run();
