using Attendances.API.Consumers;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddCarter();
builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

builder.Services.AddHttpClient();

builder.Services.AddSingleton<IDbConnectionFactory>(sp =>
{
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


var awsBucketName = builder.Configuration["AWS_BUCKET_FACEID_NAME"]
                 ?? builder.Configuration["AWS:BucketName"];
var awsRegion = builder.Configuration["AWS_REGION"]
             ?? builder.Configuration["AWS:Region"];
var awsAccessKey = builder.Configuration["AWS_ACCESS_KEY"]
                ?? builder.Configuration["AWS:AccessKey"];
var awsSecretKey = builder.Configuration["AWS_SECRET_KEY"]
                ?? builder.Configuration["AWS:SecretKey"];
var awsFolderPrefix = builder.Configuration["AWS:FolderPrefix"] ?? "attendances";

if (string.IsNullOrWhiteSpace(awsRegion) || awsRegion.StartsWith("${"))
{
    throw new InvalidOperationException($"AWS_REGION is not configured properly. Value: '{awsRegion}'. Please set AWS_REGION environment variable.");
}
if (string.IsNullOrWhiteSpace(awsBucketName) || awsBucketName.StartsWith("${"))
{
    throw new InvalidOperationException($"AWS_BUCKET_FACEID_NAME is not configured properly. Value: '{awsBucketName}'. Please set AWS_BUCKET_FACEID_NAME environment variable.");
}
if (string.IsNullOrWhiteSpace(awsAccessKey) || awsAccessKey.StartsWith("${") ||
    string.IsNullOrWhiteSpace(awsSecretKey) || awsSecretKey.StartsWith("${"))
{
    throw new InvalidOperationException("AWS credentials are not configured properly. Please set AWS_ACCESS_KEY and AWS_SECRET_KEY environment variables.");
}

awsBucketName = awsBucketName.Trim();

if (awsBucketName.Contains(" ") || awsBucketName.Contains("_"))
{
    throw new InvalidOperationException($"Invalid S3 bucket name: '{awsBucketName}'. Bucket names cannot contain spaces or underscores.");
}

Console.WriteLine($"AWS S3 Config - Region: {awsRegion}, Bucket: {awsBucketName}, Prefix: {awsFolderPrefix}");

builder.Services.Configure<Attendances.API.Extensions.AwsS3Settings>(options =>
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
builder.Services.AddScoped<Attendances.API.Extensions.IS3Service, Attendances.API.Extensions.S3Service>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<CreateAttendanceRecordConsumer>();
    x.AddConsumer<CancelAttendanceRecordConsumer>(); 
    x.AddRequestClient<GetShiftLocationRequest>(); 
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

        Console.WriteLine($"RabbitMQ Config - Host: {rabbitMqHost}, Username: {rabbitMqUsername}");
        
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
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
    if (dbFactory is MySqlConnectionFactory mysqlFactory)
    {
        await mysqlFactory.EnsureTablesCreatedAsync();
        Console.WriteLine("Attendances database tables initialized successfully");
    }
}

app.UseGlobalExceptionHandler();

app.MapCarter();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/", () => "Attendances API - Attendance & Leave Management Service");

app.Run();
