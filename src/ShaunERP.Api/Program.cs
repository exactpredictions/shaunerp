using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ShaunERP.Infrastructure;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedAsync();
}

var disableSwagger = string.Equals(
    builder.Configuration["DISABLE_SWAGGER"], "true", StringComparison.OrdinalIgnoreCase);
if (!disableSwagger)
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

app.MapGet("/", () => Results.Content(
    """
    <!DOCTYPE html>
    <html lang="en">
    <head>
      <meta charset="utf-8" />
      <meta name="viewport" content="width=device-width, initial-scale=1" />
      <title>ShaunERP Demo</title>
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
        <h1>ShaunERP</h1>
        <p>Public demo API for Shaun Infomedia. Explore endpoints in Swagger, or check service health.</p>
        <div class="links">
          <a href="/swagger">Open Swagger</a>
          <a class="secondary" href="/health">Health</a>
          <a class="secondary" href="https://shauninfomedia.com/">Company site</a>
        </div>
      </main>
    </body>
    </html>
    """,
    "text/html"));

app.Run();

public partial class Program;
