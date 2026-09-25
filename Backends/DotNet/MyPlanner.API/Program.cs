using dotenv.net;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using MyPlanner.Data.DBContexts;
using MyPlanner.Data.UnitOfWork;
using Microsoft.Extensions.Options;
using MyPlanner.Service;
using MyPlanner.Service.Interfaces;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MyPlanner.API;
using MyPlanner.API.ExceptionHandlers;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

// Load .env file for local development (environment variables override appsettings.json)
DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);
ConfigureServices(builder.Services);

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});
builder.Services.AddHttpClient();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
// Serialize enums as their names (e.g. DataOrigin.Receipt, not 2) so the
// frontend contract stays stable regardless of enum ordering.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "MyPlanner API";
        document.Info.Version = "v1";
        return Task.CompletedTask;
    });
});

builder.Services.AddAuthorization();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = builder.Configuration["Jwt:Issuer"];
                options.Audience = builder.Configuration["Jwt:Audience"];

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                };
            });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();

// Post-build configuration to set up the FixedOpenIdConnectConfigurationRetriever
var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
var jwtOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
jwtOptions.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
    jwtOptions.Authority,
    new FixedOpenIdConnectConfigurationRetriever(httpClientFactory),
    new HttpDocumentRetriever { RequireHttps = true });

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();


void ConfigureServices(IServiceCollection services)
{
    var connectionString = ApplicationDbContext.GetConnectionString();

    services.AddDbContext<DbContext, ApplicationDbContext>(
            dbContextOptions => dbContextOptions
                .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
                // The following three options help with debugging, but should
                // be changed or removed for production.
                .LogTo(Console.WriteLine, LogLevel.Information)
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors()
        );

    services.AddScoped<IUnitOfWork, UnitOfWork>();
    services.AddTransient<IPageService, PageService>();
    services.AddTransient<ITodoTaskService, TodoTaskService>();
    services.AddTransient<INoteService, NoteService>();
    services.AddTransient<ITodoTaskSessionService, TodoTaskSessionService>();

    // Register LlmSettings as typed options from configuration
    services.Configure<MyPlanner.Service.Models.LlmSettings>(builder.Configuration.GetSection("LlmSettings"));
    services.AddScoped<ILlmService, LlmService>();
    services.AddScoped<IFinanceService, FinanceService>();

}
