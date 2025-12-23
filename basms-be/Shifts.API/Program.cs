var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCarter();

builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

builder.Services.AddSingleton<IDbConnectionFactory>(sp =>
{
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

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<EmailHandler>();
builder.Services.AddScoped<ShiftValidator>();


var awsBucketName = builder.Configuration["AWS_BUCKET_NAME"]
                 ?? builder.Configuration["AWS:BucketName"];
var awsRegion = builder.Configuration["AWS_REGION"]
             ?? builder.Configuration["AWS:Region"];
var awsAccessKey = builder.Configuration["AWS_ACCESS_KEY"]
                ?? builder.Configuration["AWS:AccessKey"];
var awsSecretKey = builder.Configuration["AWS_SECRET_KEY"]
                ?? builder.Configuration["AWS:SecretKey"];
var awsFolderPrefix = builder.Configuration["AWS:FolderPrefix"] ?? "shifts/evidence";


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


builder.Services.Configure<AwsS3Settings>(options =>
{
    options.BucketName = awsBucketName;
    options.Region = awsRegion;
    options.AccessKey = awsAccessKey;
    options.SecretKey = awsSecretKey;
    options.FolderPrefix = awsFolderPrefix;
});

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
builder.Services.AddScoped<IS3Service, S3Service>();
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<Shifts.API.Consumers.UserCreatedConsumer>();
    x.AddConsumer<Shifts.API.Consumers.UserUpdatedConsumer>();
    x.AddConsumer<Shifts.API.Consumers.UserDeletedConsumer>();
    x.AddConsumer<Shifts.API.Consumers.ContractActivatedConsumer>();
    x.AddConsumer<Shifts.API.Consumers.UpdateGuardInfoConsumer>();
    x.AddConsumer<Shifts.API.Consumers.UpdateManagerInfoConsumer>();
    x.AddConsumer<Shifts.API.Consumers.DeactivateGuardConsumer>();
    x.AddConsumer<Shifts.API.Consumers.GetShiftLocationConsumer>(); 
    Console.WriteLine("Registered GetShiftLocationConsumer for Request/Response");
    
    x.AddRequestClient<CheckPublicHolidayRequest>();
    x.AddRequestClient<CheckLocationClosedRequest>();
    x.AddRequestClient<GetContractShiftSchedulesRequest>();
    x.AddRequestClient<GetCustomerByContractRequest>();
    
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
        
        if (string.IsNullOrWhiteSpace(rabbitMqHost))
        {
            throw new InvalidOperationException("RabbitMQ Host is not configured properly");
        }

        cfg.Host(rabbitMqHost, h =>
        {
            h.Username(rabbitMqUsername);
            h.Password(rabbitMqPassword);
        });
        
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
        
        cfg.ConfigureEndpoints(context);
        
        cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
    });
});


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
builder.Services.AddHostedService<Shifts.API.BackgroundJobs.AutoGenerateShiftsJob>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
    if (dbFactory is MySqlConnectionFactory mysqlFactory)
    {
        await mysqlFactory.EnsureTablesCreatedAsync();
        Console.WriteLine("Shifts database tables initialized successfully");
    }
}
app.UseGlobalExceptionHandler();

app.MapCarter();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/", () => "Shifts API - Shift & Team Management Service");

app.Run();