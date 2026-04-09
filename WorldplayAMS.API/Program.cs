using Supabase;
using WorldplayAMS.API.Services;
using WorldplayAMS.Core.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

// Supabase Configuration
var supabaseUrl = builder.Configuration["Supabase:Url"] ?? "https://placeholder.supabase.co";
var supabaseKey = builder.Configuration["Supabase:Key"] ?? "placeholder_key";

var options = new SupabaseOptions
{
    AutoRefreshToken = true,
    AutoConnectRealtime = true
};

builder.Services.AddSingleton(provider => new Supabase.Client(supabaseUrl, supabaseKey, options));

// Local Services
builder.Services.AddScoped<IFallbackCacheService, FallbackCacheService>();
builder.Services.AddScoped<SessionManagerService>();
builder.Services.AddScoped<MachineMonitoringService>();
builder.Services.AddScoped<IGameSessionService, GameSessionService>();
builder.Services.AddScoped<IRfidReaderService, RfidReaderService>();

var app = builder.Build();

// Initialize Supabase (fetch schema, connect realtime if configured)
using (var scope = app.Services.CreateScope())
{
    var client = scope.ServiceProvider.GetRequiredService<Supabase.Client>();
    await client.InitializeAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Minimal API Endpoints

app.MapPost("/api/sessions/start", async (StartSessionDto request, IGameSessionService sessionService) =>
{
    var session = await sessionService.StartSessionAsync(request.TagUid, request.MachineId);
    if (session == null) return Results.BadRequest("Invalid Tag or Machine");
    return Results.Ok(session);
})
.WithName("StartSession")
.WithOpenApi();

app.MapGet("/api/sessions/active", async (SessionManagerService sessionService) =>
{
    var sessions = await sessionService.GetActiveSessionsAsync();
    var dtos = sessions.Select(s => new { s.Id, s.RfidTagId, s.StartTime, s.Status, s.TotalDurationMinutes, s.Fee });
    return Results.Ok(dtos);
})
.WithName("GetActiveSessions")
.WithOpenApi();

app.MapGet("/api/rfid/{tagUid}", async (string tagUid, IRfidReaderService rfidService) =>
{
    var tag = await rfidService.ValidateTagAsync(tagUid);
    if (tag == null) return Results.NotFound();
    return Results.Ok(new { tag.Id, tag.TagString, tag.UserId, tag.Status });
})
.WithName("ValidateTag")
.WithOpenApi();

app.MapPost("/api/sessions/process-tap", async (ProcessTapDto request, SessionManagerService sessionService) =>
{
    var result = await sessionService.ProcessRfidTapAsync(request.TagString);
    return Results.Ok(result);
})
.WithName("ProcessTap")
.WithOpenApi();

app.MapPost("/api/machines/toggle", async (ToggleMachineDto request, MachineMonitoringService machineService) =>
{
    var result = await machineService.ProcessMachineToggleAsync(request.MachineId);
    return Results.Ok(result);
})
.WithName("ToggleMachine")
.WithOpenApi();

app.MapGet("/api/machines", async (MachineMonitoringService machineService) =>
{
    var result = await machineService.GetAllMachinesAsync();
    var dtos = result.Select(m => new { m.Id, m.Name, m.MachineType, m.Status });
    return Results.Ok(dtos);
})
.WithName("GetMachines")
.WithOpenApi();

app.MapGet("/api/sessions/history", async (SessionManagerService sessionService) =>
{
    var result = await sessionService.GetCompletedSessionsAsync();
    var dtos = result.Select(s => new { s.Id, s.RfidTagId, s.StartTime, s.Status, s.TotalDurationMinutes, s.Fee });
    return Results.Ok(dtos);
})
.WithName("GetSessionHistory")
.WithOpenApi();

app.MapGet("/api/sessions/revenue/today", async (SessionManagerService sessionService) =>
{
    var result = await sessionService.GetTodayRevenueAsync();
    return Results.Ok(result);
})
.WithName("GetTodayRevenue")
.WithOpenApi();

app.MapGet("/api/machines/logs", async (MachineMonitoringService machineService) =>
{
    var result = await machineService.GetUsageLogsAsync();
    var dtos = result.Select(m => new { m.Id, m.MachineId, m.StartTime, m.EndTime, m.Status });
    return Results.Ok(dtos);
})
.WithName("GetMachineUsageLogs")
.WithOpenApi();

app.MapPost("/api/seed", async (Supabase.Client client) =>
{
    var logs = new List<string>();
    try
    {
        var tag = new WorldplayAMS.Core.Models.RfidTag
        {
            Id = Guid.NewGuid(),
            TagString = "DEMO-TAG-001",
            UserId = null,
            Status = "Active"
        };
        await client.From<WorldplayAMS.Core.Models.RfidTag>().Insert(tag);
        logs.Add("RFID tag seeded successfully.");
    } catch (Exception ex) { logs.Add("RFID Error: " + ex.Message); }

    try
    {
        var machineId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var machine = new WorldplayAMS.Core.Models.ArcadeMachine
        {
            Id = machineId,
            Name = "Cyber Racer Terminal",
            MachineType = "Racing",
            Status = "Online"
        };
        await client.From<WorldplayAMS.Core.Models.ArcadeMachine>().Insert(machine);
        logs.Add("Arcade machine seeded successfully.");
    } catch (Exception ex) { logs.Add("Machine Error: " + ex.Message); }

    return Results.Ok(logs);
});

app.Run();

// DTOs
public record StartSessionDto(string TagUid, Guid MachineId);
public record ProcessTapDto(string TagString);
public record ToggleMachineDto(Guid MachineId);
