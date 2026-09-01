using FormGen.Application.Services;
using FormGen.Infrastructure.Persistence;
using FormGen.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using NewDynamicFormGenAPI.API.Middleware;
using NewDynamicFormGenAPI.Models.Interfaces;
using NewDynamicFormGenAPI.Models.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Database (Database-First: connection string points at the DB created from database/01_Schema.sql) ----
builder.Services.AddDbContext<FormGenDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAutoMapper(cfg => { }, typeof(Program).Assembly);

// ---- DI: repositories / services ----
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IFormService, FormService>();
builder.Services.AddScoped<IRuleEngineService, RuleEngineService>();
builder.Services.AddScoped<ISubmissionService, SubmissionService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();

// ---- CORS for the Angular dev server ----
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

app.UseMiddleware<ExceptionHandlingMiddleware>();


app.UseHttpsRedirection();
app.UseCors("AngularClient");
app.MapControllers();

app.Run();
