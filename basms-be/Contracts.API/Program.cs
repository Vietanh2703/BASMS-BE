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

// Đăng ký MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    // Register all consumers
    x.AddConsumer<Contracts.API.Consumers.UserCreatedConsumer>();
    x.AddConsumer<Contracts.API.Consumers.UserUpdatedConsumer>();
    x.AddConsumer<Contracts.API.Consumers.UserDeletedConsumer>();
    x.AddConsumer<Contracts.API.Consumers.ShiftsGeneratedConsumer>();

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

// Map Carter endpoints
app.MapCarter();
Console.WriteLine("✓ Carter endpoints mapped");

app.MapGet("/", () => "Contracts API - Customer & Contract Management Service");

app.Run();