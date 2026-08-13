using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ShaunERP.Core.ImportExport;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using ShaunERP.Infrastructure.Services.ImportExport;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration["Storage:ProjectFilesRoot"] ??=
    Path.Combine(builder.Environment.ContentRootPath, "App_Data", "project-files");

builder.Services.AddInfrastructure(builder.Configuration);

var jwt = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key missing"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var enableSwagger =
    builder.Environment.IsDevelopment()
    || string.Equals(builder.Configuration["ENABLE_SWAGGER"], "true", StringComparison.OrdinalIgnoreCase);
var disableSwagger = string.Equals(
    builder.Configuration["DISABLE_SWAGGER"], "true", StringComparison.OrdinalIgnoreCase);
if (enableSwagger && !disableSwagger)
{
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "ShaunERP API",
            Version = "v1",
            Description = "Open-source ERP MVP — Auth, CRM, Finance"
        });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme.",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });
}

var corsOrigins = builder.Configuration.GetSection("Jwt:Cors:Origins").Get<string[]>()
    ?? builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? ["https://erp.shauninfomedia.com"];

builder.Services.AddCors(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.AddPolicy("AllowAll", policy =>
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
    }
    else
    {
        options.AddPolicy("AllowAll", policy =>
            policy.WithOrigins(corsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod());
    }
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedAsync();
}

if (enableSwagger && !disableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseMiddleware<ShaunERP.Api.Middleware.ForbiddenExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var tenant = context.RequestServices.GetRequiredService<ITenantContext>();
        var authz = context.RequestServices.GetRequiredService<IAuthorizationContextService>();
        if (tenant.UserId is null)
        {
            var snapshot = await authz.BuildFromPrincipalAsync(context.User);
            tenant.Set(snapshot);
        }
    }

    await next();
});

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", product = "ShaunERP", time = DateTime.UtcNow }));

// One-shot production bootstrap (disabled unless BOOTSTRAP_TOKEN is configured).
var bootstrapToken = builder.Configuration["BOOTSTRAP_TOKEN"]
    ?? Environment.GetEnvironmentVariable("BOOTSTRAP_TOKEN");
if (!string.IsNullOrWhiteSpace(bootstrapToken))
{
    app.MapPost("/internal/bootstrap-users", async (
        HttpRequest request,
        IProductionUserBootstrapService bootstrap,
        ProductionUserBootstrapRequest body) =>
    {
        if (!request.Headers.TryGetValue("X-Bootstrap-Token", out var provided)
            || !string.Equals(provided.ToString(), bootstrapToken, StringComparison.Ordinal))
        {
            return Results.Unauthorized();
        }

        var result = await bootstrap.BootstrapUsersAsync(body);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    });

    app.MapPost("/internal/production-init", async (
        HttpRequest request,
        IProductionInitializationService init,
        ITenantContext tenant) =>
    {
        if (!request.Headers.TryGetValue("X-Bootstrap-Token", out var provided)
            || !string.Equals(provided.ToString(), bootstrapToken, StringComparison.Ordinal))
        {
            return Results.Unauthorized();
        }

        tenant.Set(new TenantContextSnapshot
        {
            UserId = 1,
            IsSystemAdmin = true,
            CompanyId = null,
            CompanyName = null,
            Permissions = Permissions.AdminDefaults.ToHashSet(StringComparer.OrdinalIgnoreCase),
            Entitlements = ModuleCatalog.All.Select(m => m.Key).ToHashSet(StringComparer.OrdinalIgnoreCase)
        });

        var initialized = await init.InitializeProductionDatasetAsync();
        var removed = await init.RemoveDemoDataAsync();
        return Results.Ok(new { initialized, removed });
    });
}

app.MapGet("/", () => Results.Content(
    """
    <!DOCTYPE html>
    <html lang="en">
    <head>
      <meta charset="utf-8" />
      <meta name="viewport" content="width=device-width, initial-scale=1" />
      <title>ShaunERP API</title>
      <style>
        :root { color-scheme: light; }
        body { margin: 0; font-family: "IBM Plex Sans", system-ui, sans-serif; background: #f4f6f8; color: #1c2430; }
        main { max-width: 40rem; margin: 0 auto; padding: 4rem 1.5rem; }
        h1 { font-size: clamp(2.4rem, 6vw, 3.4rem); font-weight: 650; margin: 0 0 0.75rem; }
        p { line-height: 1.55; color: #5b6777; }
        a { color: #0f5f8a; }
        .links { display: flex; flex-wrap: wrap; gap: 0.75rem; margin-top: 1.5rem; }
        .links a {
          text-decoration: none; font-weight: 600;
          padding: 0.7rem 1rem; border-radius: 8px; border: 1px solid rgba(28,36,48,.12);
          background: #0f5f8a; color: #fff;
        }
        .links a.secondary { background: transparent; color: #1c2430; }
      </style>
    </head>
    <body>
      <main>
        <h1>ShaunERP API</h1>
        <p>Operational API for Shaun Infomedia OPC Private Limited. Use the Blazor web app at erp.shauninfomedia.com for interactive access.</p>
        <div class="links">
          <a class="secondary" href="/health">Health</a>
          <a class="secondary" href="https://erp.shauninfomedia.com/">ERP Web</a>
          <a class="secondary" href="https://shauninfomedia.com/">Company site</a>
        </div>
      </main>
    </body>
    </html>
    """,
    "text/html"));

app.Run();

public partial class Program;
