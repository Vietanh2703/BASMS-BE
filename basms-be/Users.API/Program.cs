using Users.API.UsersHandler.UpdateOtp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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

// Add JWT Settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

// Add Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();
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
        ValidIssuer = jwtSettings?.Issuer,
        ValidAudience = jwtSettings?.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.SecretKey ?? ""))
    };
});

// Add Authorization
builder.Services.AddAuthorization();

// Add HttpContextAccessor for accessing user claims in handlers
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Add Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapCarter();
app.Run();