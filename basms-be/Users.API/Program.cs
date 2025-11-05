using Users.API.UsersHandler.UpdateOtp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCarter();

builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

// Register validators manually
builder.Services.AddScoped<CreateUserValidator>();
builder.Services.AddScoped<DeleteUserValidator>();
builder.Services.AddScoped<UpdateUserValidator>();
builder.Services.AddScoped<CreateOtpValidator>();
builder.Services.AddScoped<VerifyOtpValidator>();
builder.Services.AddScoped<UpdateOtpValidator>();
builder.Services.AddScoped<UpdatePasswordValidator>();
builder.Services.AddScoped<ConfirmPasswordChangeValidator>();

// Register Dapper connection factory for MySQL
builder.Services.AddSingleton<IDbConnectionFactory>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("Database")!;
    return new MySqlConnectionFactory(connectionString);
});
// Cấu hình Firebase
var firebaseConfig = builder.Configuration.GetSection("FIREBASE_CONFIG").Get<Dictionary<string, string>>();
var jsonConfig = JsonSerializer.Serialize(firebaseConfig);
var credential = GoogleCredential.FromJson(jsonConfig);

FirebaseApp.Create(new AppOptions
{
    Credential = credential
});

// Register EmailSettings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// Register EmailHandler
builder.Services.AddScoped<EmailHandler>();

var app = builder.Build();
app.MapCarter();
app.Run();