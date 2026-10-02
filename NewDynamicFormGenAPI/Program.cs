using FormGen.Application.Services;
using FormGen.Infrastructure.Persistence;
using FormGen.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using NewDynamicFormGenAPI.API.Middleware;
using NewDynamicFormGenAPI.Models.Interfaces;
using NewDynamicFormGenAPI.Models.Services;

/// <summary>
/// ASP.NET Core application entry point and configuration.
/// </summary>
/// <remarks>
/// <para>
/// This file configures the Form Generation API application, including:
/// <list type="bullet">
///   <item><description>Database connection and Entity Framework Core configuration</description></item>
///   <item><description>Dependency injection for services, repositories, and middleware</description></item>
///   <item><description>CORS policy for Angular frontend development server communication</description></item>
///   <item><description>File upload size limits (10 MB max)</description></item>
///   <item><description>AutoMapper configuration for DTO mapping</description></item>
///   <item><description>HTTP logging and exception handling middleware</description></item>
/// </list>
/// </para>
/// <para>
/// No authentication/authorization is currently configured. If access control is needed in the future,
/// add JWT or other auth mechanisms here using AddAuthentication() and register [Authorize] on protected endpoints.
/// </para>
/// </remarks>

var builder = WebApplication.CreateBuilder(args);

// ---- Database (Database-First: connection string points at the DB created from database/01_Schema.sql) ----
/// <summary>
/// Configures Entity Framework Core with SQL Server database connection.
/// </summary>
builder.Services.AddDbContext<FormGenDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

/// <summary>
/// Configures file upload size limits for form submissions with file attachments.
/// </summary>
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024;   // 10 MB
});

/// <summary>
/// Configures AutoMapper for automatic DTO to entity mapping.
/// </summary>
builder.Services.AddAutoMapper(cfg => { }, typeof(Program).Assembly);

// ---- DI: repositories / services ----
/// <summary>
/// Registers the Unit of Work pattern for coordinating repository and transaction management.
/// </summary>
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

/// <summary>
/// Registers business logic services.
/// </summary>
builder.Services.AddScoped<IFormService, FormService>();
builder.Services.AddScoped<IRuleEngineService, RuleEngineService>();
builder.Services.AddScoped<ISubmissionService, SubmissionService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();

// ---- CORS for the Angular dev server ----
/// <summary>
/// Configures CORS policy to enable communication between ASP.NET API and Angular frontend development server.
/// </summary>
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularClient", policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                            ?? new[] { "http://localhost:4200" })
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// No authentication in this application — every endpoint is open. If access control is ever
// needed later (e.g. only for the form-builder screens, not the public fill-in link), add
// JWT/auth back in here and put [Authorize] only on the controllers that need it.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

/// <summary>
/// Registers global exception handling middleware.
/// </summary>
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.UseCors("AngularClient");
app.MapControllers();

app.Run();
