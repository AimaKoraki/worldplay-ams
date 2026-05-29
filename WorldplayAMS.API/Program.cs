using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Supabase;
using WorldplayAMS.API.Services;
using WorldplayAMS.Core.Interfaces;
using WorldplayAMS.Core.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

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
builder.Services.AddScoped<ISupabaseRepository, SupabaseRepository>();
builder.Services.AddScoped<SessionManagerService>();
builder.Services.AddScoped<MachineMonitoringService>();
builder.Services.AddScoped<IGameSessionService, GameSessionService>();
builder.Services.AddScoped<TransactionHistoryService>();
builder.Services.AddScoped<DigitalReceiptService>();
builder.Services.AddScoped<IRfidReaderService, RfidReaderService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<StaffService>();

builder.Services.AddSingleton<ExportJobQueue>();
builder.Services.AddSingleton<ExportJobStateTracker>();
builder.Services.AddHostedService<BackgroundExportService>();
builder.Services.AddScoped<ExportDataService>();

builder.Services.AddAuthentication("Supabase")
    .AddScheme<AuthenticationSchemeOptions, SupabaseAuthHandler>("Supabase", null);

builder.Services.AddAuthorization(options => 
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("AdminOrTech", policy => policy.RequireRole("Admin", "Technician"));
    options.AddPolicy("AdminOrStaff", policy => policy.RequireRole("Admin", "Staff"));
});

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

app.UseAuthentication();
app.UseAuthorization();

// Minimal API Endpoints

app.MapPost("/api/sessions/start", async (StartSessionDto request, IGameSessionService sessionService) =>
{
    var session = await sessionService.StartSessionAsync(request.TagUid, request.MachineId);
    if (session == null) return Results.BadRequest("Invalid Tag or Machine");
    return Results.Ok(session);
})
.WithName("StartSession")
.RequireAuthorization("AdminOrStaff")
.WithOpenApi();

app.MapGet("/api/sessions/active", async (SessionManagerService sessionService, MachineMonitoringService machineService) =>
{
    var sessions = await sessionService.GetActiveSessionsAsync();

    // DEV-20: Resolve MachineId → MachineName so staff can identify machines in billing disputes
    var allMachines = await machineService.GetAllMachinesAsync();
    var machineMap = allMachines.ToDictionary(m => m.Id, m => m.Name);

    var dtos = sessions.Select(s => new
    {
        s.Id, s.RfidTagId, s.StartTime, s.Status, s.TotalDurationMinutes,
        s.Fee, s.GuestName, s.CheckedOutByStaff, s.MachineId,
        MachineName = s.MachineId.HasValue && machineMap.TryGetValue(s.MachineId.Value, out var name) ? name : null
    });
    return Results.Ok(dtos);
})
.WithName("GetActiveSessions")
.RequireAuthorization("AdminOrStaff")
.WithOpenApi();

app.MapGet("/api/rfid/{tagUid}", async (string tagUid, IRfidReaderService rfidService) =>
{
    var tag = await rfidService.ValidateTagAsync(tagUid);
    if (tag == null) return Results.NotFound();
    return Results.Ok(new { tag.Id, tag.TagString, tag.UserId, tag.Status });
})
.WithName("ValidateTag")
.RequireAuthorization("AdminOrStaff")
.WithOpenApi();

app.MapPost("/api/sessions/process-tap", async (ProcessTapDto request, SessionManagerService sessionService) =>
{
    var result = await sessionService.ProcessRfidTapAsync(request.TagString, request.StaffName, request.GuestName, request.MachineId, request.StaffId);
    return Results.Ok(result);
})
.WithName("ProcessTap")
.RequireAuthorization("AdminOrStaff")
.WithOpenApi();

app.MapPost("/api/machines/toggle", async (ToggleMachineDto request, MachineMonitoringService machineService) =>
{
    var result = await machineService.ProcessMachineToggleAsync(request.MachineId, request.TechnicianName ?? "Unknown Technician", request.StaffId);
    return Results.Ok(result);
})
.WithName("ToggleMachine")
.RequireAuthorization("AdminOrTech")
.WithOpenApi();

app.MapGet("/api/machines", async (MachineMonitoringService machineService) =>
{
    var result = await machineService.GetAllMachinesAsync();
    var dtos = result.Select(m => new { m.Id, m.Name, m.MachineType, m.Status, FeePerMinute = m.CurrentCostPerPlay });
    return Results.Ok(dtos);
})
.WithName("GetMachines")
.RequireAuthorization("AdminOrStaff")
.WithOpenApi();

app.MapPost("/api/machines", async (CreateMachineDto request, WorldplayAMS.Core.Interfaces.ISupabaseRepository repository) =>
{
    try
    {
        var machine = new WorldplayAMS.Core.Models.ArcadeMachine
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            MachineType = request.MachineType,
            Status = "Online",
            CurrentCostPerPlay = request.FeePerMinute
        };
        await repository.InsertMachineAsync(machine);
        return Results.Ok(new { machine.Id, machine.Name, machine.MachineType, machine.Status, FeePerMinute = machine.CurrentCostPerPlay });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
})
.WithName("CreateMachine")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapPut("/api/machines/{id}", async (Guid id, UpdateMachineDto request, WorldplayAMS.Core.Interfaces.ISupabaseRepository repository) =>
{
    try
    {
        var machine = await repository.GetMachineAsync(id);
        if (machine == null) return Results.NotFound();

        machine.Name = request.Name;
        machine.MachineType = request.MachineType;
        machine.Status = request.Status;
        machine.CurrentCostPerPlay = request.FeePerMinute;

        await repository.UpdateMachineAsync(machine);
        return Results.Ok(new { message = "Machine updated successfully." });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
})
.WithName("UpdateMachine")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapDelete("/api/machines/{id}", async (Guid id, WorldplayAMS.Core.Interfaces.ISupabaseRepository repository) =>
{
    try
    {
        await repository.DeleteMachineAsync(id);
        return Results.Ok(new { message = "Machine deleted successfully." });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
})
.WithName("DeleteMachine")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapGet("/api/sessions/history", async (SessionManagerService sessionService, MachineMonitoringService machineService) =>
{
    var result = await sessionService.GetCompletedSessionsAsync();

    // DEV-20: Resolve MachineId → MachineName server-side so staff can identify machines in dispute resolution
    var allMachines = await machineService.GetAllMachinesAsync();
    var machineMap = allMachines.ToDictionary(m => m.Id, m => m.Name);

    var dtos = result.Select(s => new
    {
        s.Id, s.RfidTagId, s.StartTime, s.EndTime, s.Status,
        s.TotalDurationMinutes, s.Fee, s.GuestName, s.CheckedOutByStaff,
        s.MachineId,
        MachineName = s.MachineId.HasValue && machineMap.TryGetValue(s.MachineId.Value, out var name) ? name : null
    });
    return Results.Ok(dtos);
})
.WithName("GetSessionHistory")
.RequireAuthorization("AdminOrStaff")
.WithOpenApi();

app.MapGet("/api/sessions/revenue/today", async (SessionManagerService sessionService) =>
{
    var result = await sessionService.GetTodayRevenueAsync();
    return Results.Ok(result);
})
.WithName("GetTodayRevenue")
.RequireAuthorization("AdminOrStaff")
.WithOpenApi();

app.MapGet("/api/machines/logs", async (MachineMonitoringService machineService) =>
{
    var result = await machineService.GetUsageLogsAsync();
    var dtos = result.Select(m => new { m.Id, m.MachineId, m.StartTime, m.EndTime, m.Status });
    return Results.Ok(dtos);
})
.WithName("GetMachineUsageLogs")
.RequireAuthorization("AdminOrStaff")
.WithOpenApi();

// DEV-16: Auth Proxy Endpoints

app.MapPost("/api/auth/login", async (LoginDto request, Supabase.Client client, WorldplayAMS.API.Services.TransactionHistoryService txnService) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Results.BadRequest(new { error = "Email and password are required." });

        // Authenticate via Supabase Auth (GoTrue)
        var authResponse = await client.Auth.SignIn(request.Email, request.Password);
        if (authResponse?.User == null)
            return Results.Unauthorized();

        // Look up user role from Users table
        var userRecord = await client.From<UserContext>()
            .Where(u => u.Email == request.Email)
            .Single();

        var userName = userRecord?.Name ?? authResponse.User.Email ?? "Unknown";
        var userRole = userRecord?.SystemRole ?? "Staff";
        var token = authResponse?.AccessToken;

        return Results.Ok(new
        {
            name = userName,
            email = request.Email,
            role = userRole,
            authenticated = true,
            token = token
        });
    }
    catch (Supabase.Gotrue.Exceptions.GotrueException)
    {
        // DEV-17: Audit log — Failed login attempt
        try
        {
            await txnService.LogManagerActionAsync("SYSTEM", "LOGIN_FAILED", $"Failed login attempt for: {request.Email}");
        }
        catch { /* best effort */ }
        return Results.Json(new { error = "Invalid email or password." }, statusCode: 401);
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = "Authentication service unavailable: " + ex.Message }, statusCode: 500);
    }
})
.WithName("Login")
.WithOpenApi();

app.MapPost("/api/auth/logout", () =>
{
    return Results.Ok(new { message = "Logged out successfully." });
})
.WithName("Logout")
.WithOpenApi();

// Seed endpoint — Development only
if (app.Environment.IsDevelopment())
{
app.MapPost("/api/seed", async (Supabase.Client client) =>
{
    var logs = new List<string>();
    try
    {
        // Add Tags
        var suffix = Guid.NewGuid().ToString()[..6].ToUpper();
        var t1 = new WorldplayAMS.Core.Models.RfidTag { TagString = $"TAG-{suffix}-1", Status = "Active" };
        var t2 = new WorldplayAMS.Core.Models.RfidTag { TagString = $"TAG-{suffix}-2", Status = "Active" };
        
        var tagsRes = await client.From<WorldplayAMS.Core.Models.RfidTag>().Insert(new List<WorldplayAMS.Core.Models.RfidTag> { t1, t2 });
        var tag1 = tagsRes.Models[0];
        var tag2 = tagsRes.Models[1];
        logs.Add("RFID tags seeded successfully.");

        // Add Machines
        var mc1 = new WorldplayAMS.Core.Models.ArcadeMachine { Name = $"Cyber Racer {suffix}", MachineType = "Racing", Status = "Online", CurrentCostPerPlay = 15.00m };
        var mc2 = new WorldplayAMS.Core.Models.ArcadeMachine { Name = $"VR Arena {suffix}", MachineType = "VR", Status = "Online", CurrentCostPerPlay = 15.00m };
        var mc3 = new WorldplayAMS.Core.Models.ArcadeMachine { Name = $"Neon Hoops {suffix}", MachineType = "Sports", Status = "Online", CurrentCostPerPlay = 15.00m };
        
        var machinesRes = await client.From<WorldplayAMS.Core.Models.ArcadeMachine>().Insert(new List<WorldplayAMS.Core.Models.ArcadeMachine> { mc1, mc2, mc3 });
        var m1 = machinesRes.Models[0];
        var m2 = machinesRes.Models[1];
        var m3 = machinesRes.Models[2];
        logs.Add("Arcade machines seeded successfully.");

        // Add Sessions & Receipts (Historical Data)
        var random = new Random();
        var sessions = new List<WorldplayAMS.Core.Models.Session>();
        var receipts = new List<WorldplayAMS.Core.Models.DigitalReceipt>();
        
        for (int i = 0; i < 20; i++)
        {
            var daysAgo = random.Next(0, 14);
            var duration = random.Next(15, 120);
            var machine = i % 3 == 0 ? m1 : (i % 3 == 1 ? m2 : m3);
            var tag = i % 2 == 0 ? tag1 : tag2;
            var fee = duration * 15m; // Simulated LKR

            var start = DateTime.UtcNow.AddDays(-daysAgo).AddHours(-random.Next(1, 10));
            var end = start.AddMinutes(duration);

            var session = new WorldplayAMS.Core.Models.Session
            {
                Id = Guid.NewGuid(),
                RfidTagId = tag.Id,
                MachineId = machine.Id,
                StartTime = start,
                EndTime = end,
                Status = "Completed",
                TotalDurationMinutes = duration,
                Fee = fee,
                GuestName = $"Demo Guest {i}",
                CheckedOutByStaff = "Admin"
            };
            sessions.Add(session);
        } // close for loop

        var sessionsRes = await client.From<WorldplayAMS.Core.Models.Session>().Insert(sessions);
        
        for (int i = 0; i < sessionsRes.Models.Count; i++)
        {
            var insertedSession = sessionsRes.Models[i];
            receipts.Add(new WorldplayAMS.Core.Models.DigitalReceipt
            {
                SessionId = insertedSession.Id,
                ReceiptNumber = $"RCPT-{DateTime.UtcNow:yyyyMMdd}-{random.Next(1000, 9999)}",
                RfidTagId = insertedSession.RfidTagId,
                GuestName = insertedSession.GuestName,
                MachineName = "Arcade Machine", // simplified
                CheckInTime = insertedSession.StartTime,
                CheckOutTime = insertedSession.EndTime ?? DateTime.UtcNow,
                DurationMinutes = insertedSession.TotalDurationMinutes ?? 0,
                Fee = insertedSession.Fee ?? 0,
                StaffName = "Admin",
                IssuedAt = insertedSession.EndTime ?? DateTime.UtcNow,
                Status = "Issued"
            });
        }

        await client.From<WorldplayAMS.Core.Models.DigitalReceipt>().Insert(receipts);
        logs.Add($"Seeded {sessions.Count} historical sessions and receipts.");
    }
    catch (Exception ex) { logs.Add("Error during seeding: " + ex.Message); }

    return Results.Ok(logs);
});
}

// DEV-13: Transaction History & Audit Endpoints

app.MapGet("/api/transactions", async (DateTime? from, DateTime? to, TransactionHistoryService txnService) =>
{
    var fromDate = from ?? DateTime.UtcNow.Date;
    var toDate = to ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);
    var result = await txnService.GetTransactionsByDateRangeAsync(fromDate, toDate);
    var dtos = result.Select(s => new { s.Id, s.RfidTagId, s.StartTime, s.EndTime, s.Status, s.TotalDurationMinutes, s.Fee, s.CheckedOutByStaff });
    return Results.Ok(dtos);
})
.WithName("GetTransactions")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapGet("/api/transactions/summary", async (DateTime? date, TransactionHistoryService txnService) =>
{
    var targetDate = date ?? DateTime.UtcNow.Date;
    var result = await txnService.GetDailyReconciliationSummaryAsync(targetDate);
    return Results.Ok(result);
})
.WithName("GetTransactionSummary")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapPost("/api/audit/log", async (AuditLogDto request, TransactionHistoryService txnService) =>
{
    await txnService.LogManagerActionAsync(request.ManagerName, request.Action, request.Details, request.StaffId);
    return Results.Ok("Audit log recorded.");
})
.WithName("PostAuditLog")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapGet("/api/audit/logs", async (int? limit, TransactionHistoryService txnService) =>
{
    var result = await txnService.GetManagerAuditLogsAsync(limit ?? 50);
    var dtos = result.Select(l => new { l.Id, l.ManagerName, l.Action, l.Details, l.Timestamp });
    return Results.Ok(dtos);
})
.WithName("GetAuditLogs")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

// DEV-8: Digital Receipt Endpoints

app.MapGet("/api/receipts", async (DateTime? from, DateTime? to, DigitalReceiptService receiptService) =>
{
    var fromDate = from ?? DateTime.UtcNow.Date;
    var toDate = to ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);
    var result = await receiptService.GetReceiptsByDateRangeAsync(fromDate, toDate);
    var dtos = result.Select(r => new { r.Id, r.SessionId, r.ReceiptNumber, r.RfidTagId,
        r.GuestName, r.MachineName, r.CheckInTime, r.CheckOutTime, r.DurationMinutes,
        r.Fee, r.StaffName, r.IssuedAt, r.Status });
    return Results.Ok(dtos);
})
.WithName("GetReceipts")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapGet("/api/receipts/search", async (string? q, DigitalReceiptService receiptService) =>
{
    var query = q ?? "";
    var result = await receiptService.SearchReceiptsAsync(query);
    var dtos = result.Select(r => new { r.Id, r.SessionId, r.ReceiptNumber, r.RfidTagId,
        r.GuestName, r.MachineName, r.Fee, r.IssuedAt, r.Status });
    return Results.Ok(dtos);
})
.WithName("SearchReceipts")
.RequireAuthorization("AdminOrStaff")
.WithOpenApi();

app.MapGet("/api/receipts/{sessionId}", async (Guid sessionId, DigitalReceiptService receiptService) =>
{
    var result = await receiptService.GetReceiptBySessionAsync(sessionId);
    if (result == null) return Results.NotFound("No receipt found for this session.");
    return Results.Ok(new { result.Id, result.SessionId, result.ReceiptNumber, result.RfidTagId,
        result.GuestName, result.MachineName, result.CheckInTime, result.CheckOutTime,
        result.DurationMinutes, result.Fee, result.StaffName, result.IssuedAt, result.Status });
})
.WithName("GetReceiptBySession")
.RequireAuthorization("AdminOrStaff")
.WithOpenApi();

app.MapPost("/api/receipts/{sessionId}/email", async (Guid sessionId, EmailRequestDto request, DigitalReceiptService receiptService, IEmailService emailService) =>
{
    if (string.IsNullOrWhiteSpace(request.Email)) return Results.BadRequest("Email address is required.");
    
    var receipt = await receiptService.GetReceiptBySessionAsync(sessionId);
    if (receipt == null) return Results.NotFound("No receipt found for this session.");

    var success = await emailService.SendReceiptEmailAsync(request.Email, receipt);
    if (success)
    {
        return Results.Ok(new { message = "Email sent successfully." });
    }
    return Results.StatusCode(500); // Internal server error if it failed
})
.WithName("EmailReceipt")
.RequireAuthorization("AdminOrStaff")
.WithOpenApi();

// DEV-52: Staff Management Endpoints (Admin only)

app.MapGet("/api/staff", async (WorldplayAMS.API.Services.StaffService staffService) =>
{
    try
    {
        var users = await staffService.GetAllStaffAsync();
        var dtos = users.Select(u => new
        {
            u.Id, u.Name, u.Email, u.SystemRole,
            u.FirstName, u.LastName
        });
        return Results.Ok(dtos);
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
})
.WithName("GetStaff")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapPost("/api/staff", async (CreateStaffDto request, WorldplayAMS.API.Services.StaffService staffService, WorldplayAMS.API.Services.TransactionHistoryService txnService) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.Name))
        return Results.BadRequest(new { error = "Email, password, and name are required." });

    var validRoles = new[] { "Admin", "Staff", "Technician" };
    if (!validRoles.Contains(request.Role))
        return Results.BadRequest(new { error = "Role must be Admin, Staff, or Technician." });

    try
    {
        var result = await staffService.RegisterStaffAsync(request.Email, request.Password, request.Name, request.Role);
        if (!result.Success)
        {
            return Results.Json(new { error = result.ErrorMessage }, statusCode: 500);
        }

        // DEV-17: Audit log — Staff created
        try
        {
            await txnService.LogManagerActionAsync("Admin", "STAFF_CREATED", $"Created {request.Role} account: {request.Name} ({request.Email})");
        }
        catch { /* best effort */ }

        return Results.Ok(new { id = result.AuthUserId, name = request.Name, email = request.Email, role = request.Role });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
})
.WithName("CreateStaff")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapPut("/api/staff/{id}/role", async (Guid id, UpdateRoleDto request, WorldplayAMS.API.Services.StaffService staffService, WorldplayAMS.API.Services.TransactionHistoryService txnService) =>
{
    var validRoles = new[] { "Admin", "Staff", "Technician" };
    if (!validRoles.Contains(request.Role))
        return Results.BadRequest(new { error = "Role must be Admin, Staff, or Technician." });

    try
    {
        var success = await staffService.ChangeRoleAsync(id, request.Role);
        if (!success) return Results.Json(new { error = "Failed to update role." }, statusCode: 500);

        // DEV-17: Audit log — Role changed
        try
        {
            await txnService.LogManagerActionAsync("Admin", "ROLE_CHANGED", $"User {id} role changed to {request.Role}");
        }
        catch { /* best effort */ }

        return Results.Ok(new { message = "Role updated successfully." });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
})
.WithName("UpdateStaffRole")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapDelete("/api/staff/{id}", async (Guid id, WorldplayAMS.API.Services.StaffService staffService, WorldplayAMS.API.Services.TransactionHistoryService txnService) =>
{
    try
    {
        var error = await staffService.DeleteStaffAsync(id);
        if (error != null)
        {
            return Results.Json(new { error }, statusCode: 500);
        }

        // DEV-17: Audit log — Staff deleted
        try
        {
            await txnService.LogManagerActionAsync("Admin", "STAFF_DELETED", $"Deleted staff account: {id}");
        }
        catch { /* best effort */ }

        return Results.Ok(new { message = "Staff account deleted." });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
})
.WithName("DeleteStaff")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

// DEV-9: Analytics Endpoints

app.MapGet("/api/analytics/peak-hours", async (DateTime? from, DateTime? to, AnalyticsService analyticsService) =>
{
    var fromDate = from ?? DateTime.UtcNow.Date;
    var toDate = to ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);
    var result = await analyticsService.GetPeakHoursAsync(fromDate, toDate);
    return Results.Ok(result);
})
.WithName("GetPeakHours")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapGet("/api/analytics/machine-usage", async (DateTime? from, DateTime? to, AnalyticsService analyticsService) =>
{
    var fromDate = from ?? DateTime.UtcNow.Date;
    var toDate = to ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);
    var result = await analyticsService.GetMachineUsageAnalyticsAsync(fromDate, toDate);
    return Results.Ok(result);
})
.WithName("GetMachineUsage")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

// DEV-24: Staffing Recommendations
app.MapGet("/api/analytics/staffing-recommendations", async (DateTime? from, DateTime? to, AnalyticsService analyticsService) =>
{
    var fromDate = from ?? DateTime.UtcNow.Date.AddDays(-30); // Default to last 30 days for historical trends
    var toDate = to ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);
    var result = await analyticsService.GetStaffingRecommendationsAsync(fromDate, toDate);
    return Results.Ok(result);
})
.WithName("GetStaffingRecommendations")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

// DEV-15a: RevPAMH Calculation
app.MapGet("/api/analytics/revpamh", async (DateTime? from, DateTime? to, string? category, AnalyticsService analyticsService) =>
{
    var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
    var toDate = to ?? DateTime.UtcNow;
    var result = await analyticsService.GetRevPAMHAsync(fromDate, toDate, category);
    return result != null ? Results.Ok(result) : Results.StatusCode(500);
})
.RequireAuthorization("AdminOnly")
.WithName("GetRevPAMH")
.WithOpenApi();

// DEV-XX: Data Export Endpoints
app.MapPost("/api/exports/request", async (ExportJobRequest request, ExportJobStateTracker tracker, ExportJobQueue queue, ISupabaseRepository repository, ExportDataService exportDataService) =>
{
    var jobId = Guid.NewGuid();
    var status = new ExportJobStatus { JobId = jobId, Status = "Pending" };
    tracker.AddJob(status);

    ExportDataService.JobRequests[jobId] = request;

    // Estimate dataset size
    int recordCount = 0;
    if (request.Category == "Transactions" || request.Category == "Sales")
    {
        recordCount = await repository.GetSessionsCountByDateRangeAsync(request.FromDate, request.ToDate.AddDays(1).AddTicks(-1));
    }
    else if (request.Category == "Machines" || request.Category == "Inventory")
    {
        recordCount = await repository.GetMachinesCountAsync();
    }
    else if (request.Category == "AuditLogs" || request.Category == "User Logs")
    {
        recordCount = await repository.GetAuditLogsCountAsync();
    }

    if (recordCount > 50000)
    {
        // Send to background queue
        await queue.QueueJobAsync(jobId);
        return Results.Accepted($"/api/exports/status/{jobId}", status);
    }
    else
    {
        // Process synchronously
        status.Status = "Processing";
        await exportDataService.ProcessJobAsync(jobId, default);
        var completedStatus = tracker.GetJob(jobId);
        return Results.Ok(completedStatus);
    }
})
.WithName("RequestExport")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapGet("/api/exports/status/{jobId}", (Guid jobId, ExportJobStateTracker tracker) =>
{
    var status = tracker.GetJob(jobId);
    if (status == null) return Results.NotFound();
    return Results.Ok(status);
})
.WithName("GetExportStatus")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapGet("/api/exports/download/{jobId}", (Guid jobId, ExportJobStateTracker tracker) =>
{
    var status = tracker.GetJob(jobId);
    if (status == null || status.Status != "Completed" || status.FilePath == null) 
        return Results.NotFound("File not ready or job not found.");
        
    var bytes = System.IO.File.ReadAllBytes(status.FilePath);
    
    // Clean up file after reading into memory (optional, but good for temp cleanup)
    try { System.IO.File.Delete(status.FilePath); } catch { }

    return Results.File(bytes, status.ContentType ?? "application/octet-stream", status.FileName);
})
.WithName("DownloadExport")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.Run();

// DTOs
public record StartSessionDto(string TagUid, Guid MachineId);
public record ProcessTapDto(string TagString, string? StaffName = null, string? GuestName = null, Guid? MachineId = null, Guid? StaffId = null);
public record ToggleMachineDto(Guid MachineId, string? TechnicianName = null, Guid? StaffId = null);
public record AuditLogDto(string ManagerName, string Action, string? Details = null, Guid? StaffId = null);
public record EmailRequestDto(string Email);
public record LoginDto(string Email, string Password);
public record CreateStaffDto(string Email, string Password, string Name, string Role);
public record UpdateRoleDto(string Role);

public record CreateMachineDto(string Name, string MachineType, decimal? FeePerMinute);
public record UpdateMachineDto(string Name, string MachineType, string Status, decimal? FeePerMinute);

// DEV-52: Minimal projection of the Supabase Admin API user-creation response
public class SupabaseAdminUserResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string? Id { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("email")]
    public string? Email { get; set; }
}
