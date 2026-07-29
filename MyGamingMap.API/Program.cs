using MyGamingMap.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
//builder.Services.AddOpenApi();

// For testing API endpoints in Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<PSNService>();
builder.Services.AddScoped<IGDBService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<MapService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();
app.MapControllers();
app.Run();