using System.Text;
using System.Text.Json.Serialization;
using Amazon.S3;
using MediVault.Api.Auth;
using MediVault.Api.Care;
using MediVault.Api.Data;
using MediVault.Api.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddDbContext<MediVaultDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
var storage = builder.Configuration.GetSection("Storage").Get<StorageOptions>()
    ?? throw new InvalidOperationException("Storage settings are missing.");
builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
    storage.AccessKey,
    storage.SecretKey,
    new AmazonS3Config
    {
        ServiceURL = storage.ServiceUrl,
        ForcePathStyle = true
    }));
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("paypal");
builder.Services.AddHttpClient("openai", client => client.Timeout = TimeSpan.FromSeconds(90));
builder.Services.AddSingleton<PayPalService>();
builder.Services.AddSingleton<ClinicalAidService>();
builder.Services.AddScoped<ObjectStorage>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<CareService>();
builder.Services.AddHostedService<OpinionDeadlineWorker>();
builder.Services.AddScoped<AdminSeeder>();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT settings are missing.");
if (jwt.Key.Length < 32)
{
    throw new InvalidOperationException("JWT key must be at least 32 characters.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MediVaultDbContext>();
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            break;
        }
        catch (Exception) when (attempt < 30)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }

    await scope.ServiceProvider.GetRequiredService<AdminSeeder>().SeedAsync(CancellationToken.None);
    await scope.ServiceProvider.GetRequiredService<ObjectStorage>().EnsureBucketAsync(CancellationToken.None);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
