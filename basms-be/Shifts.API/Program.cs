using Amazon;
using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime;
using Amazon.S3;
using BuildingBlocks.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký Carter - Library để tổ chức API endpoints theo module
builder.Services.AddCarter();

// Đăng ký MediatR - Library để implement CQRS pattern
// Tự động scan và đăng ký tất cả handlers trong assembly
builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

// Đăng ký Dapper connection factory cho MySQL
// Singleton vì connection factory có thể tái sử dụng
builder.Services.AddSingleton<IDbConnectionFactory>(sp =>
{
    // Ưu tiên đọc từ environment variable trực tiếp
    var connectionString = builder.Configuration["DB_CONNECTION_STRING_SHIFTS"]
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

// Đăng ký EmailSettings từ appsettings.json
builder.Services.Configure<Shifts.API.ExtendModels.EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

// Đăng ký EmailHandler - Gửi email notifications cho guards
builder.Services.AddScoped<Shifts.API.Extensions.EmailHandler>();

// Đăng ký ShiftValidator - Validator cho shift validation (overlap, contract period)
builder.Services.AddScoped<Shifts.API.Validators.ShiftValidator>();

// ================================================================
// 🆕 ĐĂNG KÝ AWS S3 CHO FILE UPLOAD (CHỨNG TỪ NGHỈ VIỆC)
// ================================================================
var awsBucketName = builder.Configuration["AWS_BUCKET_NAME"]
                 ?? builder.Configuration["AWS:BucketName"];
var awsRegion = builder.Configuration["AWS_REGION"]
             ?? builder.Configuration["AWS:Region"];
var awsAccessKey = builder.Configuration["AWS_ACCESS_KEY"]
                ?? builder.Configuration["AWS:AccessKey"];
var awsSecretKey = builder.Configuration["AWS_SECRET_KEY"]
                ?? builder.Configuration["AWS:SecretKey"];
var awsFolderPrefix = builder.Configuration["AWS:FolderPrefix"] ?? "shifts/evidence";

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
builder.Services.Configure<Shifts.API.Extensions.AwsS3Settings>(options =>
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
builder.Services.AddScoped<Shifts.API.Extensions.IS3Service, Shifts.API.Extensions.S3Service>();

// Đăng ký MassTransit with RabbitMQ and Consumers
builder.Services.AddMassTransit(x =>
{
    // Register all consumers
    x.AddConsumer<Shifts.API.Consumers.UserCreatedConsumer>();
    x.AddConsumer<Shifts.API.Consumers.UserUpdatedConsumer>();
    x.AddConsumer<Shifts.API.Consumers.UserDeletedConsumer>();
    x.AddConsumer<Shifts.API.Consumers.ContractActivatedConsumer>();
    x.AddConsumer<Shifts.API.Consumers.UpdateGuardInfoConsumer>();
    x.AddConsumer<Shifts.API.Consumers.UpdateManagerInfoConsumer>();
    x.AddConsumer<Shifts.API.Consumers.DeactivateGuardConsumer>();
    x.AddConsumer<Shifts.API.Consumers.GetShiftLocationConsumer>(); // Request/Response consumer
    Console.WriteLine("✓ Registered GetShiftLocationConsumer for Request/Response");

    // Request Clients for Auto-Generate Shifts feature (from BuildingBlocks.Messaging.Contracts)
    x.AddRequestClient<CheckPublicHolidayRequest>();
    x.AddRequestClient<CheckLocationClosedRequest>();
    x.AddRequestClient<GetContractShiftSchedulesRequest>();
    x.AddRequestClient<GetCustomerByContractRequest>();

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
        
        // Mỗi service cần queue riêng để cùng nhận UserCreatedEvent (publish/subscribe pattern)
        cfg.ReceiveEndpoint("shifts-api-user-created", e =>
        {
            e.ConfigureConsumer<Shifts.API.Consumers.UserCreatedConsumer>(context);
        });

        cfg.ReceiveEndpoint("shifts-api-user-updated", e =>
        {
            e.ConfigureConsumer<Shifts.API.Consumers.UserUpdatedConsumer>(context);
        });

        cfg.ReceiveEndpoint("shifts-api-user-deleted", e =>
        {
            e.ConfigureConsumer<Shifts.API.Consumers.UserDeletedConsumer>(context);
        });

        cfg.ReceiveEndpoint("shifts-api-contract-activated", e =>
        {
            e.ConfigureConsumer<Shifts.API.Consumers.ContractActivatedConsumer>(context);
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

// ================================================================
// BACKGROUND JOBS
// ================================================================
// Auto-generate shifts job - chạy daily lúc 2:00 AM để check và tạo shifts
// OPTIMIZED version: không query shifts_db.contracts, mà query Contracts.API qua RabbitMQ
builder.Services.AddHostedService<Shifts.API.BackgroundJobs.AutoGenerateShiftsJob>();

var app = builder.Build();

// Initialize database tables
using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
    if (dbFactory is MySqlConnectionFactory mysqlFactory)
    {
        await mysqlFactory.EnsureTablesCreatedAsync();
        Console.WriteLine("✓ Shifts database tables initialized successfully");
    }
}

// Thêm Global Exception Handler Middleware
// Middleware này phải đặt đầu tiên để catch tất cả exceptions
app.UseGlobalExceptionHandler();

app.MapCarter();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/", () => "Shifts API - Shift & Team Management Service");

app.Run();