using Amazon.Extensions.NETCore.Setup;
using BuildingBlocks.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký Carter - Library để tổ chức API endpoints theo module
builder.Services.AddCarter();

// Đăng ký MediatR - Library để implement CQRS pattern
builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

// Đăng ký Dapper connection factory cho MySQL
builder.Services.AddSingleton<IDbConnectionFactory>(sp =>
{
    // Ưu tiên đọc từ environment variable trực tiếp
    var connectionString = builder.Configuration["DB_CONNECTION_STRING_CONTRACTS"]
                        ?? builder.Configuration["ConnectionStrings__Database"]
                        ?? builder.Configuration.GetConnectionString("Database");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Database connection string is not configured properly");
    }

    Console.WriteLine($"Database Connection String: Server={ExtractServerFromConnectionString(connectionString)}");
    return new MySqlConnectionFactory(connectionString);
});

// Helper method để extract server từ connection string cho logging
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

// Đăng ký EmailSettings và EmailHandler
builder.Services.Configure<Contracts.API.Extensions.EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<Contracts.API.Extensions.EmailHandler>();

// Đăng ký GoongSettings cho geocoding API
builder.Services.Configure<Contracts.API.Extensions.GoongSettings>(
    builder.Configuration.GetSection("GoongSettings"));

// Đăng ký AWS S3
var awsBucketName = builder.Configuration["AWS_BUCKET_NAME"]
                 ?? builder.Configuration["AWS:BucketName"];
var awsRegion = builder.Configuration["AWS_REGION"]
             ?? builder.Configuration["AWS:Region"];
var awsAccessKey = builder.Configuration["AWS_ACCESS_KEY"]
                ?? builder.Configuration["AWS:AccessKey"];
var awsSecretKey = builder.Configuration["AWS_SECRET_KEY"]
                ?? builder.Configuration["AWS:SecretKey"];
var awsFolderPrefix = builder.Configuration["AWS:FolderPrefix"] ?? "contracts";

// Validate AWS configuration
if (string.IsNullOrWhiteSpace(awsRegion))
{
    throw new InvalidOperationException("AWS_REGION is not configured. Please set AWS_REGION environment variable.");
}
if (string.IsNullOrWhiteSpace(awsBucketName))
{
    throw new InvalidOperationException("AWS_BUCKET_NAME is not configured. Please set AWS_BUCKET_NAME environment variable.");
}
if (string.IsNullOrWhiteSpace(awsAccessKey) || string.IsNullOrWhiteSpace(awsSecretKey))
{
    throw new InvalidOperationException("AWS credentials are not configured. Please set AWS_ACCESS_KEY and AWS_SECRET_KEY environment variables.");
}

Console.WriteLine($"AWS S3 Config - Region: {awsRegion}, Bucket: {awsBucketName}, Prefix: {awsFolderPrefix}");

// Bind AWS settings manually
builder.Services.Configure<Contracts.API.Extensions.AwsS3Settings>(options =>
{
    options.BucketName = awsBucketName;
    options.Region = awsRegion;
    options.AccessKey = awsAccessKey;
    options.SecretKey = awsSecretKey;
    options.FolderPrefix = awsFolderPrefix;
});

// Configure AWS S3 Client
var awsOptions = new AWSOptions
{
    Credentials = new BasicAWSCredentials(awsAccessKey, awsSecretKey),
    Region = RegionEndpoint.GetBySystemName(awsRegion)
};

if (awsOptions.Region == null)
{
    throw new InvalidOperationException($"Invalid AWS Region: '{awsRegion}'. Valid examples: ap-southeast-2, us-east-1, eu-west-1");
}

builder.Services.AddDefaultAWSOptions(awsOptions);
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddScoped<Contracts.API.Extensions.IS3Service, Contracts.API.Extensions.S3Service>();

// Đăng ký Word Contract Service và Digital Signature Service
builder.Services.AddScoped<Contracts.API.Extensions.IWordContractService, Contracts.API.Extensions.WordContractService>();
builder.Services.AddScoped<Contracts.API.Extensions.IDigitalSignatureService, Contracts.API.Extensions.DigitalSignatureService>();

// Đăng ký Background Job để xử lý contract tự động (thay thế AWS Lambda)
builder.Services.AddScoped<Contracts.API.BackgroundJobs.ContractProcessingJob>();

// Đăng ký MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    // Register all consumers
    x.AddConsumer<Contracts.API.Consumers.UserCreatedConsumer>();
    x.AddConsumer<Contracts.API.Consumers.UserUpdatedConsumer>();
    x.AddConsumer<Contracts.API.Consumers.UserDeletedConsumer>();
    x.AddConsumer<Contracts.API.Consumers.ShiftsGeneratedConsumer>();
    x.AddConsumer<Contracts.API.Consumers.GetContractShiftSchedulesConsumer>(); // Request/Response consumer

    // Register Request Clients for calling other services
    x.AddRequestClient<CreateUserRequest>();

    // Configure RabbitMQ
    x.UsingRabbitMq((context, cfg) =>
    {
        // Ưu tiên đọc từ environment variables trực tiếp (RABBITMQ_HOST)
        // Fallback về RabbitMQ:Host (nested config) nếu không có
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

        // Validate host không empty
        if (string.IsNullOrWhiteSpace(rabbitMqHost))
        {
            throw new InvalidOperationException("RabbitMQ Host is not configured properly");
        }

        cfg.Host(rabbitMqHost, h =>
        {
            h.Username(rabbitMqUsername);
            h.Password(rabbitMqPassword);
        });

        // ✅ FIX: Configure endpoint với queue name riêng cho Contracts.API
        // Mỗi service cần queue riêng để cùng nhận UserCreatedEvent (publish/subscribe pattern)
        cfg.ReceiveEndpoint("contracts-api-user-created", e =>
        {
            e.ConfigureConsumer<Contracts.API.Consumers.UserCreatedConsumer>(context);
        });

        cfg.ReceiveEndpoint("contracts-api-user-updated", e =>
        {
            e.ConfigureConsumer<Contracts.API.Consumers.UserUpdatedConsumer>(context);
        });

        cfg.ReceiveEndpoint("contracts-api-user-deleted", e =>
        {
            e.ConfigureConsumer<Contracts.API.Consumers.UserDeletedConsumer>(context);
        });

        cfg.ReceiveEndpoint("contracts-api-shifts-generated", e =>
        {
            e.ConfigureConsumer<Contracts.API.Consumers.ShiftsGeneratedConsumer>(context);
        });

        // Configure other endpoints
        cfg.ConfigureEndpoints(context);

        // Configure retry policy
        cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
    });
});


// Cấu hình CORS cho frontend
// Đọc từ ALLOWED_ORIGINS env var (string phân cách bằng dấu phẩy) hoặc AllowedOrigins section
var allowedOriginsString = builder.Configuration["ALLOWED_ORIGINS"]
                         ?? builder.Configuration["AllowedOrigins"]
                         ?? "";

var allowedOrigins = string.IsNullOrWhiteSpace(allowedOriginsString)
    ? new[] { "http://localhost:3000" } // Fallback cho development
    : allowedOriginsString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

Console.WriteLine($"CORS Allowed Origins: {string.Join(", ", allowedOrigins)}");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            // Thay đổi địa chỉ cấu hình frontend để backend kết nối được tới frontend
            policy.WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
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

var app = builder.Build();

// Initialize database tables
using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
    if (dbFactory is MySqlConnectionFactory mysqlFactory)
    {
        await mysqlFactory.EnsureTablesCreatedAsync();
        Console.WriteLine("✓ Contracts database tables initialized successfully");
    }
}

// Thêm Global Exception Handler Middleware
// Middleware này phải đặt đầu tiên để catch tất cả exceptions
app.UseGlobalExceptionHandler();

app.MapCarter();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/", () => "Contracts API - Customer & Contract Management Service");

app.Run();