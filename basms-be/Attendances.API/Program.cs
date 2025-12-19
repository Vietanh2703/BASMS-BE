using Amazon;
using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime;
using BuildingBlocks.Extensions;
using Attendances.API.Consumers;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký Carter - Library để tổ chức API endpoints theo module
builder.Services.AddCarter();

// Đăng ký MediatR - Library để implement CQRS pattern
// Tự động scan và đăng ký tất cả handlers trong assembly
builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

// Đăng ký HttpClient Factory cho Face Recognition API calls
builder.Services.AddHttpClient();

// Đăng ký Dapper connection factory cho MySQL
// Singleton vì connection factory có thể tái sử dụng
builder.Services.AddSingleton<IDbConnectionFactory>(sp =>
{
    // Ưu tiên đọc từ environment variable trực tiếp
    var connectionString = builder.Configuration["DB_CONNECTION_STRING_ATTENDANCES"]
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

// Đăng ký AWS S3
var awsBucketName = builder.Configuration["AWS_BUCKET_FACEID_NAME"]
                 ?? builder.Configuration["AWS:BucketName"];
var awsRegion = builder.Configuration["AWS_REGION"]
             ?? builder.Configuration["AWS:Region"];
var awsAccessKey = builder.Configuration["AWS_ACCESS_KEY"]
                ?? builder.Configuration["AWS:AccessKey"];
var awsSecretKey = builder.Configuration["AWS_SECRET_KEY"]
                ?? builder.Configuration["AWS:SecretKey"];
var awsFolderPrefix = builder.Configuration["AWS:FolderPrefix"] ?? "attendances";

// Validate AWS configuration
if (string.IsNullOrWhiteSpace(awsRegion))
{
    throw new InvalidOperationException("AWS_REGION is not configured. Please set AWS_REGION environment variable.");
}
if (string.IsNullOrWhiteSpace(awsBucketName))
{
    throw new InvalidOperationException("AWS_BUCKET_FACEID_NAME is not configured. Please set AWS_BUCKET_FACEID_NAME environment variable.");
}
if (string.IsNullOrWhiteSpace(awsAccessKey) || string.IsNullOrWhiteSpace(awsSecretKey))
{
    throw new InvalidOperationException("AWS credentials are not configured. Please set AWS_ACCESS_KEY and AWS_SECRET_KEY environment variables.");
}

Console.WriteLine($"AWS S3 Config - Region: {awsRegion}, Bucket: {awsBucketName}, Prefix: {awsFolderPrefix}");

// Bind AWS settings manually
builder.Services.Configure<Attendances.API.Extensions.AwsS3Settings>(options =>
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
builder.Services.AddScoped<Attendances.API.Extensions.IS3Service, Attendances.API.Extensions.S3Service>();

// Đăng ký MassTransit with RabbitMQ and Consumers
builder.Services.AddMassTransit(x =>
{
    // Register consumers
    x.AddConsumer<CreateAttendanceRecordConsumer>();
    x.AddConsumer<CancelAttendanceRecordConsumer>(); // 🆕 Consumer để sync khi assignment bị cancel

    // Request Clients for querying other services
    // Example:
    // x.AddRequestClient<GetGuardInfoRequest>();
    // x.AddRequestClient<GetShiftInfoRequest>();

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

        // Mỗi service cần queue riêng để cùng nhận events (publish/subscribe pattern)
        // Example endpoints:
        // cfg.ReceiveEndpoint("attendances-api-shift-assignment-created", e =>
        // {
        //     e.ConfigureConsumer<ShiftAssignmentCreatedConsumer>(context);
        // });

        // Configure other endpoints
        cfg.ConfigureEndpoints(context);

        // Configure retry policy
        cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
    });
});

// Cấu hình CORS cho frontend và mobile app
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
            // Thay đổi địa chỉ cấu hình frontend/mobile để backend kết nối được
            policy.WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
});

// JWT Authentication
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
// BACKGROUND JOBS (if needed)
// ================================================================
// Example: Auto-calculate daily attendance summary
// builder.Services.AddHostedService<DailyAttendanceSummaryJob>();

var app = builder.Build();

// Initialize database tables
using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
    if (dbFactory is MySqlConnectionFactory mysqlFactory)
    {
        await mysqlFactory.EnsureTablesCreatedAsync();
        Console.WriteLine("✓ Attendances database tables initialized successfully");
    }
}

// Thêm Global Exception Handler Middleware
// Middleware này phải đặt đầu tiên để catch tất cả exceptions
app.UseGlobalExceptionHandler();

app.MapCarter();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/", () => "Attendances API - Attendance & Leave Management Service");

app.Run();
