using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using SiteChat.Backend.Api.Configuration;
using SiteChat.Backend.Api.Middleware;
using SiteChat.Backend.Api.Security;
using SiteChat.Backend.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

var siteChatOptions = builder.Configuration.GetSection(SiteChatOptions.SectionName).Get<SiteChatOptions>() ?? new SiteChatOptions();
if (siteChatOptions.IsProduction && !siteChatOptions.Jwt.IsSecure)
{
    throw new InvalidOperationException("Refusing to start in production with an insecure JWT secret.");
}

builder.Services.AddOptions<SiteChatOptions>()
    .Bind(builder.Configuration.GetSection(SiteChatOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => !options.IsProduction || options.Jwt.IsSecure, "Production requires a strong JWT secret.")
    .Validate(options => AiProviderNames.IsSupported(options.Rag.LlmProvider), "The configured AI provider is not supported.")
    .ValidateOnStart();

builder.Services.AddSingleton<IClientIpResolver, ClientIpResolver>();
builder.Services.AddSingleton<IPasswordPolicy, PasswordPolicy>();
builder.Services.AddSingleton<ICrawlerUrlValidator, CrawlerUrlValidator>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddSingleton<IStaticFrontendService, StaticFrontendService>();
builder.Services.AddSingleton<IMongoSiteChatRepository, MongoSiteChatRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISiteAccessService, SiteAccessService>();
builder.Services.AddScoped<IConversationAccessService, ConversationAccessService>();
builder.Services.AddScoped<ISiteManagementService, SiteManagementService>();
builder.Services.AddScoped<IPlatformConfigurationService, PlatformConfigurationService>();
builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddScoped<ICrawlService, CrawlService>();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IOpenRouterClient, OpenRouterClient>((_, client) =>
{
    client.BaseAddress = new Uri($"{siteChatOptions.Rag.OpenRouterBaseUrl.TrimEnd('/')}/");
    client.Timeout = TimeSpan.FromSeconds(45);
});
builder.Services.AddScoped<IAiProviderClientFactory, AiProviderClientFactory>();
builder.Services.AddScoped<IAiProviderClient>(serviceProvider =>
    serviceProvider.GetRequiredService<IAiProviderClientFactory>().Create());
builder.Services.AddHostedService<StartupHostedService>();
builder.Services.AddHostedService<CrawlSchedulerHostedService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (siteChatOptions.Security.CorsOrigins.Count == 1 && siteChatOptions.Security.CorsOrigins[0] == "*")
        {
            policy.AllowAnyOrigin();
        }
        else
        {
            policy.WithOrigins(siteChatOptions.Security.CorsOrigins.ToArray());
        }

        if (!string.IsNullOrWhiteSpace(siteChatOptions.Security.CorsOriginRegex))
        {
            policy.SetIsOriginAllowed(origin => siteChatOptions.IsCorsOriginAllowed(origin));
        }

        policy.AllowAnyMethod()
            .WithHeaders("Authorization", "Content-Type", "X-Requested-With", "Accept", "Origin")
            .WithExposedHeaders("X-Total-Count", "X-Page", "X-Per-Page");

        if (siteChatOptions.Security.CorsAllowCredentials && siteChatOptions.Security.CorsOrigins is not ["*"])
        {
            policy.AllowCredentials();
        }
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(siteChatOptions.Jwt.Secret))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.Admin, policy => policy.RequireRole(UserRoles.Admin));
    options.AddPolicy(AuthorizationPolicies.AdminOrUser, policy => policy.RequireRole(UserRoles.Admin, UserRoles.User));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var resolver = context.RequestServices.GetRequiredService<IClientIpResolver>();
        var key = resolver.GetClientIp(context);
        return RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = siteChatOptions.RateLimit.Requests,
                QueueLimit = 0,
                Window = TimeSpan.FromSeconds(siteChatOptions.RateLimit.WindowSeconds)
            });
    });
});

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
});
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (!siteChatOptions.IsProduction)
{
    app.UseSwagger(options => options.RouteTemplate = "api/docs/{documentName}/swagger.json");
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "api/docs";
        options.SwaggerEndpoint("/api/docs/v1/swagger.json", "SiteChat Backend API v1");
    });
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestValidationMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (siteChatOptions.Security.TrustedHosts.Count > 0 && siteChatOptions.Security.TrustedHosts is not ["*"])
{
    app.UseHostFiltering();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseAuthorization();

var frontendRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "..", "frontend"));
if (Directory.Exists(frontendRoot))
{
    foreach (var staticFolder in new[] { "css", "js", "widget" })
    {
        var folderPath = Path.Combine(frontendRoot, staticFolder);
        if (Directory.Exists(folderPath))
        {
            app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(folderPath),
                RequestPath = "/" + staticFolder
            });
        }
    }
}

app.MapControllers();
MapStaticFrontendRoutes(app);

app.Run();

static void MapStaticFrontendRoutes(WebApplication app)
{
    app.MapGet("/", async (IStaticFrontendService frontend, CancellationToken cancellationToken) =>
        await frontend.GetHtmlOrInfoAsync("landing.html", includeWidgetSri: true, cancellationToken).ConfigureAwait(false));

    app.MapGet("/app", async (IStaticFrontendService frontend, CancellationToken cancellationToken) =>
        await frontend.GetHtmlOrInfoAsync("index.html", cancellationToken: cancellationToken).ConfigureAwait(false));

    app.MapGet("/dashboard", async (IStaticFrontendService frontend, CancellationToken cancellationToken) =>
        await frontend.GetHtmlOrInfoAsync("index.html", cancellationToken: cancellationToken).ConfigureAwait(false));

    app.MapGet("/login", async (IStaticFrontendService frontend, CancellationToken cancellationToken) =>
        await frontend.GetHtmlOrInfoAsync("login.html", cancellationToken: cancellationToken).ConfigureAwait(false));

    app.MapGet("/landing-neo", async (IStaticFrontendService frontend, CancellationToken cancellationToken) =>
        await frontend.GetHtmlOrInfoAsync("landing-neo.html", cancellationToken: cancellationToken).ConfigureAwait(false));

    app.MapGet("/demo", () => Results.Redirect("/#live-demo", permanent: true));

    app.MapGet("/api", (Microsoft.Extensions.Options.IOptions<SiteChatOptions> options) => Results.Ok(new
    {
        name = options.Value.AppName,
        version = "1.0.0",
        endpoints = new
        {
            chat = "/api/chat",
            stream = "/api/chat/stream",
            crawl = "/api/crawl",
            admin = "/api/admin",
            embed = "/api/embed/setup",
            docs = options.Value.IsProduction ? null : "/api/docs"
        }
    }));
}

/// <summary>
/// Exposes the web application entry point for integration tests.
/// </summary>
public partial class Program;
