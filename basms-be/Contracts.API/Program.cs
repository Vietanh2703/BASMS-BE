using Amazon;
using Amazon.Runtime;

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
    var connectionString = builder.Configuration.GetConnectionString("Database")!;
    return new MySqlConnectionFactory(connectionString);
});

// Đăng ký EmailSettings và EmailHandler
builder.Services.Configure<Contracts.API.Extensions.EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<Contracts.API.Extensions.EmailHandler>();

// Đăng ký AWS S3
builder.Services.Configure<Contracts.API.Extensions.AwsS3Settings>(
    builder.Configuration.GetSection("AWS"));
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddScoped<Contracts.API.Extensions.IS3Service, Contracts.API.Extensions.S3Service>();
var awsOptions = builder.Configuration.GetAWSOptions();
awsOptions.Credentials = new BasicAWSCredentials(
    builder.Configuration["AWS:AccessKey"],
    builder.Configuration["AWS:SecretKey"]
);
awsOptions.Region = RegionEndpoint.GetBySystemName(builder.Configuration["AWS:Region"]);
builder.Services.AddDefaultAWSOptions(awsOptions);

// Đăng ký MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    // Register all consumers
    x.AddConsumer<Contracts.API.Consumers.UserCreatedConsumer>();
    x.AddConsumer<Contracts.API.Consumers.UserUpdatedConsumer>();
    x.AddConsumer<Contracts.API.Consumers.UserDeletedConsumer>();
    x.AddConsumer<Contracts.API.Consumers.ShiftsGeneratedConsumer>();

    // Register Request Clients for calling other services
    x.AddRequestClient<CreateUserRequest>();

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
        Console.WriteLine("✓ Contracts database tables initialized successfully");
    }
}


app.MapCarter();
app.UseCors("AllowFrontend");
app.MapGet("/", () => "Contracts API - Customer & Contract Management Service");

app.Run();