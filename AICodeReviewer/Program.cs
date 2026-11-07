using AICodeReviewer.BackgroundEmailSerivce;
using AICodeReviewer.ReviewerHub;
using AICodeReviewer.Services;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Models.Entities;
using Repository.AIReview;
using Repository.Authentication;
using Repository.Email;
using Repository.EmailNotifications;
using Repository.GitHub;
using Repository.GitHubRepos;
using Repository.PullRequest;
using Repository.ReviewExecutionRepository;
using Repository.Settings;
using Repository.SignalRNotifier;
using Repository.Teams;
using Services.AIReview;
using Services.AiReviewJob;
using Services.Authentication;
using Services.Email;
using Services.GitHub;
using Services.GitHubPoster;
using Services.GitHubRepos;
using Services.JWT;
using Services.PullRequest;
using Services.Rag;
using Services.ReviewPipeline;
using Services.Reviews;
using Services.Rule;
using Services.Teams;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ───────────────── DATABASE ─────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(3)
    ));

// ─── HANGFIRE ─────────────────────────────────────────────────────────────────

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHangfireServer();

// ─── SIGNALR ──────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ───────────────── DEPENDENCY INJECTION ─────────────────
builder.Services.AddScoped<IAuthenticationRepository, AuthenticationRepository>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
builder.Services.AddScoped<ISettingRepository, SettingRepository>();
builder.Services.AddScoped<IEmailSenderRepository, EmailSenderRepository>();
builder.Services.AddScoped<IEmailNotificationRepository, EmailNotificationRepository>();
builder.Services.AddHostedService<EmailNotificationBackgroundService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IGitHubConnectRepository, GitHubConnectRepository>();
builder.Services.AddScoped<IGitHubConnectService, GitHubConnectService>();
builder.Services.AddScoped<IGitHubRepositoryRepository, GitHubRepositoryRepository>();
builder.Services.AddScoped<IGitHubRepositoryService, GitHubRepositoryService>();
builder.Services.AddScoped<ITeamRepository,TeamRepository>();
builder.Services.AddScoped<ITeamService,TeamService>();
builder.Services.AddScoped<IPullRequestRepository, PullRequestRepository>();
builder.Services.AddScoped<IPullRequestService, PullRequestService>();
builder.Services.AddScoped<IAiReviewJob, AiReviewJob>();
builder.Services.AddScoped<IAiReviewService, AiReviewService>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<ISignalRNotifier, SignalRNotifier>();
builder.Services.AddScoped<IReviewService, ReviewService>();
// ─── v2: RAG + inline comments + rules (repository layer = Repository project, logic = Services) ───
builder.Services.Configure<RagOptions>(builder.Configuration.GetSection("Rag"));
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection("Embedding"));

// repositories (all DB access)
builder.Services.AddScoped<Repository.Rag.IRepoIndexRepository, Repository.Rag.RepoIndexRepository>();
builder.Services.AddScoped<Repository.Rag.IRepoSettingsRepository, Repository.Rag.RepoSettingsRepository>();
builder.Services.AddScoped<IReviewExecutionRepository,ReviewExecutionRepository>();
builder.Services.AddScoped<Repository.Rules.IReviewRuleRepository, Repository.Rules.ReviewRuleRepository>();

// services
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<IVectorStore, QdrantVectorStore>();
builder.Services.AddScoped<ICodeChunker, CodeChunker>();
builder.Services.AddScoped<IRepoIndexer, RepoIndexer>();
builder.Services.AddScoped<IContextRetriever, ContextRetriever>();
builder.Services.AddScoped<IIndexRepoJob, IndexRepoJob>();
builder.Services.AddScoped<IPushEventHandler, PushEventHandler>();
builder.Services.AddScoped<IRepoInsightService, RepoInsightService>();
builder.Services.AddScoped<IReviewPipeline, ReviewPipeline>();
builder.Services.AddScoped<IGitHubReviewPoster, GitHubReviewPoster>();
builder.Services.AddScoped<IReviewRuleService, ReviewRuleService>();
builder.Services.AddScoped<Services.Eval.IEvalService, Services.Eval.EvalService>();




// ───────────────── JWT AUTHENTICATION ─────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret not found.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// ───────────────── CORS ─────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins(
                builder.Configuration["AllowedOrigins:Frontend"] ?? "http://localhost:8080",
                builder.Configuration["AllowedOrigins:Frontend2"] ?? "http://localhost:5173"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ───────────────── CONTROLLERS + SWAGGER ─────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AI Code Reviewer API",
        Version = "v1",
        Description = "AI-powered PR review system"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT Bearer token"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

app.UseHangfireDashboard("/hangfire"); // Admin dashboard
app.MapHangfireDashboard("/hangfire");
app.MapHub<ReviewHub>("/hubs/review");
// ───────────────── MIDDLEWARE ─────────────────

app.UseMiddleware<Components.Middleware.GlobalExceptionMiddleware>();

// Enable Swagger in ALL environments
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AI Code Reviewer API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();

app.UseCors("FrontendPolicy");

app.UseAuthentication();
app.UseAuthorization();

// Health Check
app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        Status = "Running",
        Application = "AI Code Reviewer API",
        Time = DateTime.UtcNow
    });
});

app.MapControllers();

app.Run();