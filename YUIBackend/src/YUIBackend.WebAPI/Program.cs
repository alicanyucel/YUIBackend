using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MyProject.Application;
using MyProject.Domain.Entities;
using MyProject.Infrastructure;
using MyProject.WebAPI.Authorization;
using MyProject.WebAPI.Middleware;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;

public partial class Program {
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ── Serilog ────────────────────────────────────────────────────────────────
        builder.Host.UseSerilog((ctx, cfg) => cfg
            .ReadFrom.Configuration(ctx.Configuration)
            .WriteTo.MSSqlServer(
                connectionString: ctx.Configuration.GetConnectionString("SqlServer")!,
                sinkOptions: new Serilog.Sinks.MSSqlServer.MSSqlServerSinkOptions
                {
                    TableName = "ApplicationLogs",
                    SchemaName = "dbo",
                    AutoCreateSqlTable = true
                }));

        // ── Application + Infrastructure ──────────────────────────────────────────
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddScoped<IPasswordHasher<InitialUser>, PasswordHasher<InitialUser>>();
        builder.Services.AddScoped<ICustomerResourceAccess, CustomerResourceAccess>();

        // ── Controllers + OpenAPI ─────────────────────────────────────────────────
        builder.Services.AddControllers();
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()));
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "YUIBackend API",
                Version = "v1",
                Description = "Müşteri, hesap ve finansal işlem endpoint'leri.",
                Contact = new OpenApiContact
                {
                    Email = "yucelalicandan@hotmail.com"
                }
            });

            options.IncludeXmlComments(Path.Combine(
                AppContext.BaseDirectory,
                $"{typeof(Program).Assembly.GetName().Name}.xml"));

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT token girin. Scalar, Authorization başlığına Bearer önekini ekler."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        });

        // ── JWT Auth ──────────────────────────────────────────────────────────────
        builder.Services.AddAuthentication("Bearer")
            .AddJwtBearer("Bearer", options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]!))
                };
                options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtBearer");
                        logger.LogWarning(context.Exception, "Bearer token validation failed.");
                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
            options.AddPolicy("Administrator", policy => policy.RequireRole("Admin"));
        });

        // ── Rate Limiting ─────────────────────────────────────────────────────────
        var rateLimitPermitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 100);
        if (rateLimitPermitLimit < 1)
            throw new InvalidOperationException("RateLimiting:PermitLimit must be greater than zero.");

        builder.Services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("fixed", o =>
            {
                o.PermitLimit = rateLimitPermitLimit;
                o.Window = TimeSpan.FromMinutes(1);
                o.QueueLimit = 10;
                o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        // ── Health Checks ─────────────────────────────────────────────────────────
        builder.Services.AddHealthChecks()
            .AddSqlServer(
                builder.Configuration.GetConnectionString("SqlServer")!,
                name: "sqlserver",
                tags: ["db", "sql"])
            .AddRedis(
                builder.Configuration.GetConnectionString("Redis")!,
                name: "redis",
                tags: ["cache"],
                timeout: TimeSpan.FromSeconds(1));

        // ── OpenTelemetry ─────────────────────────────────────────────────────────
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("MyProject.WebAPI"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddPrometheusExporter());

        // ── Exception Handler ─────────────────────────────────────────────────────
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();

        // ─────────────────────────────────────────────────────────────────────────
        var app = builder.Build();

        // ── Pipeline ──────────────────────────────────────────────────────────────
        app.UseSerilogRequestLogging();
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.MapSwagger("/swagger/{documentName}/swagger.json").AllowAnonymous();
            app.MapScalarApiReference(options => options
                .WithTitle("YUIBackend API")
                .WithOperationTitleSource(OperationTitleSource.Summary)
                .WithOpenApiRoutePattern("../swagger/{documentName}/swagger.json"))
                .AllowAnonymous();
        }

        app.UseHttpsRedirection();
        app.UseCors();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers().RequireRateLimiting("fixed");

        // ── Health Check endpoint ─────────────────────────────────────────────────
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        }).AllowAnonymous();

        // ── Prometheus metrics ────────────────────────────────────────────────────
        app.MapPrometheusScrapingEndpoint("/metrics");

        app.Run();
    }
}
//

public partial class Program { }
