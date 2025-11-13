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
    // Lấy connection string từ appsettings.json
    var connectionString = builder.Configuration.GetConnectionString("Database")!;
    return new MySqlConnectionFactory(connectionString);
});

// Đăng ký EmailSettings từ appsettings.json
builder.Services.Configure<Shifts.API.ExtendModels.EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

// Đăng ký EmailHandler - Gửi email notifications cho guards
builder.Services.AddScoped<Shifts.API.Extensions.EmailHandler>();

// Đăng ký ShiftValidator - Validator cho shift validation (overlap, contract period)
builder.Services.AddScoped<Shifts.API.Validators.ShiftValidator>();

// Đăng ký MassTransit with RabbitMQ and Consumers
builder.Services.AddMassTransit(x =>
{
    // Register all consumers
    x.AddConsumer<Shifts.API.Consumers.UserCreatedConsumer>();
    x.AddConsumer<Shifts.API.Consumers.UserUpdatedConsumer>();
    x.AddConsumer<Shifts.API.Consumers.UserDeletedConsumer>();
    x.AddConsumer<Shifts.API.Consumers.ContractActivatedConsumer>();

    // Register Request Clients for RabbitMQ Request/Response pattern
    x.AddRequestClient<Shifts.API.Messages.GetContractRequest>();
    x.AddRequestClient<Shifts.API.Messages.GetLocationRequest>();
    x.AddRequestClient<Shifts.API.Messages.GetCustomerRequest>();

    // Request Clients for Auto-Generate Shifts feature (from BuildingBlocks.Messaging.Contracts)
    x.AddRequestClient<CheckPublicHolidayRequest>();
    x.AddRequestClient<CheckLocationClosedRequest>();
    x.AddRequestClient<GetContractShiftSchedulesRequest>();

    // Configure RabbitMQ
    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitMqHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
        var rabbitMqUsername = builder.Configuration["RabbitMQ:Username"] ?? "guest";
        var rabbitMqPassword = builder.Configuration["RabbitMQ:Password"] ?? "guest";

        cfg.Host(rabbitMqHost, h =>
        {
            h.Username(rabbitMqUsername);
            h.Password(rabbitMqPassword);
        });

        // Configure endpoints
        cfg.ConfigureEndpoints(context);

        // Configure retry policy
        cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
    });
});

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

app.MapCarter();
app.UseCors("AllowFrontend");
app.MapGet("/", () => "Shifts API - Shift & Team Management Service");

app.Run();