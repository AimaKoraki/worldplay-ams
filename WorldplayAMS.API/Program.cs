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

app.MapGet("/api/sessions/active", async (IGameSessionService sessionService) =>
{
    var sessions = await sessionService.GetActiveSessionsAsync();
    return Results.Ok(sessions);
})
.WithName("GetActiveSessions")
.WithOpenApi();

app.MapGet("/api/rfid/{tagUid}", async (string tagUid, IRfidReaderService rfidService) =>
{
    var tag = await rfidService.ValidateTagAsync(tagUid);
    if (tag == null) return Results.NotFound();
    return Results.Ok(tag);
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
    return Results.Ok(result);
})
.WithName("GetMachines")
.WithOpenApi();


app.Run();

// DTOs
public record StartSessionDto(string TagUid, Guid MachineId);
public record ProcessTapDto(string TagString);
public record ToggleMachineDto(Guid MachineId);
