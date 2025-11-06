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

// Đăng ký Dapper connection factory cho MySQL
// Singleton vì connection factory có thể tái sử dụng
builder.Services.AddSingleton<IDbConnectionFactory>(sp =>
{
    // Lấy connection string từ appsettings.json
    var connectionString = builder.Configuration.GetConnectionString("Database")!;
    return new MySqlConnectionFactory(connectionString);
});

// Cấu hình Firebase Authentication
// Đọc config từ appsettings.json và tạo Firebase credential
var firebaseConfig = builder.Configuration.GetSection("FIREBASE_CONFIG").Get<Dictionary<string, string>>();
var jsonConfig = JsonSerializer.Serialize(firebaseConfig);
var credential = GoogleCredential.FromJson(jsonConfig);

// Khởi tạo Firebase App với credential
FirebaseApp.Create(new AppOptions
{
    Credential = credential
});

// Đăng ký MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    // Configure RabbitMQ
    x.UsingRabbitMq((context, cfg) =>
    {
        // Get configuration from appsettings.json or environment variables
        var rabbitMqHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
        var rabbitMqUsername = builder.Configuration["RabbitMQ:Username"] ?? "guest";
        var rabbitMqPassword = builder.Configuration["RabbitMQ:Password"] ?? "guest";

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

var app = builder.Build();

// Thêm Authentication middleware
// Middleware này đọc JWT token từ header và authenticate user
app.UseAuthentication();

// Thêm Authorization middleware
// Middleware này kiểm tra user có quyền truy cập endpoint không
app.UseAuthorization();

// Map tất cả Carter endpoints
app.MapCarter();

// Chạy ứng dụng
app.Run();