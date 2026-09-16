using MyGamingMap.API.Services;
using MyGamingMap.API.Data;
using Microsoft.EntityFrameworkCore;

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
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<PSNAnalyticsService>();
builder.Services.AddScoped<IGDBAnalyticsService>();
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