using MyGamingMap.API.Services;
using MyGamingMap.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OpenAI;
using MyGamingMap.API.Services.EraSummaries;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
//builder.Services.AddOpenApi();

// For testing API endpoints in Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<PlayerService>();
builder.Services.AddScoped<PSNService>();
builder.Services.AddScoped<IGDBService>();
builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<MapService>();
builder.Services.AddSingleton<RateLimiter>();

builder.Services.AddDbContext<MyGamingMapContext>(options =>
    options
        .UseNpgsql(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            npgsqlOptions =>
                npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
        .UseSnakeCaseNamingConvention());

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddMemoryCache();

builder.Services.AddSingleton<IChatClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var model = config["OpenAI:Model"] ?? "gpt-4.1-nano";

    return new OpenAIClient(config["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured."))
        .GetChatClient(model)
        .AsIChatClient();
});

builder.Services.AddSingleton<IEraSummaryService, LlmEraSummaryService>();

var app = builder.Build();

app.UseCors("Frontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();
app.MapControllers();
app.Run();