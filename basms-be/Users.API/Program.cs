// File cấu hình chính của ứng dụng
// Thiết lập: DI Container, Database, Firebase, Email, JWT Authentication, Authorization
var builder = WebApplication.CreateBuilder(args);

// Đăng ký Carter - Library để tổ chức API endpoints theo module
builder.Services.AddCarter();

// Đăng ký MediatR - Library để implement CQRS pattern
// Tự động scan và đăng ký tất cả handlers trong assembly
builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

// Đăng ký tất cả validators thủ công
// FluentValidation để validate input data
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

// Đăng ký Dapper connection factory cho MySQL
// Singleton vì connection factory có thể tái sử dụng
builder.Services.AddSingleton<IDbConnectionFactory>(sp =>
{
    // Ưu tiên đọc từ environment variable trực tiếp
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

        Console.WriteLine("✅ Firebase initialized successfully");
    }
    else
    {
        throw new Exception($"Firebase credentials file not found at: {credentialsPath}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ ERROR initializing Firebase: {ex.Message}");
    throw;
}

// Đăng ký MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    // Register consumers
    x.AddConsumer<Users.API.Consumers.CreateUserRequestConsumer>();

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

        // Configure endpoint names and serialization
        cfg.ConfigureEndpoints(context);
    });
});


// Đăng ký EmailSettings từ appsettings.json
// Options pattern để inject cấu hình
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// Đăng ký EmailHandler để gửi email
builder.Services.AddScoped<EmailHandler>();

// Đăng ký UserEventPublisher
builder.Services.AddScoped<Users.API.Messaging.UserEventPublisher>();

// Đăng ký JwtSettings từ appsettings.json
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

// Cấu hình JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();
builder.Services.AddAuthentication(options =>
{
    // Sử dụng JWT Bearer làm scheme mặc định
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Cấu hình validation parameters cho JWT token
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,              // Validate Issuer (người phát hành token)
        ValidateAudience = true,            // Validate Audience (người nhận token)
        ValidateLifetime = true,            // Validate token chưa hết hạn
        ValidateIssuerSigningKey = true,    // Validate chữ ký token
        ValidIssuer = jwtSettings?.Issuer,
        ValidAudience = jwtSettings?.Audience,
        // Key để verify chữ ký token
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.SecretKey ?? ""))
    };
});

// Thêm Authorization service
builder.Services.AddAuthorization();

// Đăng ký HttpContextAccessor
// Cho phép truy cập HttpContext (JWT claims) trong handlers
builder.Services.AddHttpContextAccessor();

// Cấu hình CORS cho frontend
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
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
var app = builder.Build();

// Thêm Authentication middleware
// Middleware này đọc JWT token từ header và authenticate user
app.UseAuthentication();

// Thêm Authorization middleware
// Middleware này kiểm tra user có quyền truy cập endpoint không
app.UseAuthorization();

// Map tất cả Carter endpoints
app.MapCarter();
app.UseCors("AllowFrontend");
app.Run();