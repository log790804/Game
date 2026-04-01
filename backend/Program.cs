using backend.Game01;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSingleton<Game01StoreService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseHttpsRedirection();
}

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

var api = app.MapGroup("/api");

api.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    timestamp = DateTimeOffset.UtcNow
}));

var game01 = api.MapGroup("/game01");

game01.MapGet("/", async (Game01StoreService storeService) =>
{
    var store = await storeService.GetStoreAsync();
    return Results.Ok(store);
});

game01.MapPut("/state", async (Game01State state, Game01StoreService storeService) =>
{
    var store = await storeService.SaveStateAsync(state);
    return Results.Ok(store);
});

game01.MapPost("/reset", async (Game01ResetRequest request, Game01StoreService storeService) =>
{
    var store = await storeService.ResetStateAsync(request);
    return Results.Ok(store);
});

game01.MapPost("/records", async (Game01Record record, Game01StoreService storeService) =>
{
    var store = await storeService.AppendRecordAsync(record);
    return Results.Ok(store);
});

game01.MapDelete("/records", async (Game01StoreService storeService) =>
{
    var store = await storeService.ClearRecordsAsync();
    return Results.Ok(store);
});

WeatherForecast[] GetForecast()
{
    return Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
}

api.MapGet("/weatherforecast", GetForecast)
.WithName("GetApiWeatherForecast");

app.MapGet("/weatherforecast", GetForecast)
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
