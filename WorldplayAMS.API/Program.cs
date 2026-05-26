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
    var dtos = result.Select(m => new { m.Id, m.Name, m.MachineType, m.Status });
    return Results.Ok(dtos);
})
.WithName("GetMachines")
.RequireAuthorization("AdminOrStaff")
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

app.MapPost("/api/auth/login", async (LoginDto request, Supabase.Client client) =>
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
        var token = authResponse.Session?.AccessToken;

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
            var txnService = app.Services.GetRequiredService<TransactionHistoryService>();
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
.WithOpenApi();

// DEV-22: Analytics Endpoints
app.MapGet("/api/analytics/peak-hours", async (DateTime? from, DateTime? to, AnalyticsService analyticsService) =>
{
    var f = from ?? DateTime.UtcNow.AddDays(-30);
    var t = to ?? DateTime.UtcNow;
    return Results.Ok(await analyticsService.GetPeakHoursAsync(f, t));
})
.WithName("GetPeakHours")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapGet("/api/analytics/machine-usage", async (DateTime? from, DateTime? to, AnalyticsService analyticsService) =>
{
    var f = from ?? DateTime.UtcNow.AddDays(-30);
    var t = to ?? DateTime.UtcNow;
    return Results.Ok(await analyticsService.GetMachineUsageAnalyticsAsync(f, t));
})
.WithName("GetMachineUsageAnalytics")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapGet("/api/analytics/staffing-recommendations", async (DateTime? from, DateTime? to, AnalyticsService analyticsService) =>
{
    var f = from ?? DateTime.UtcNow.AddDays(-90); // 3 months of history for better staffing recs
    var t = to ?? DateTime.UtcNow;
    return Results.Ok(await analyticsService.GetStaffingRecommendationsAsync(f, t));
})
.WithName("GetStaffingRecommendations")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapGet("/api/analytics/revpamh", async (DateTime? from, DateTime? to, AnalyticsService analyticsService) =>
{
    var f = from ?? DateTime.UtcNow.Date; // Today
    var t = to ?? DateTime.UtcNow;
    return Results.Ok(await analyticsService.GetRevPAMHAsync(f, t));
})
.WithName("GetRevPAMH")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

// DEV-52: Staff Management Endpoints

app.MapGet("/api/staff", async (StaffService staffService) =>
{
    var staff = await staffService.GetAllStaffAsync();
    var dtos = staff.Select(s => new { s.Id, s.Name, s.Email, s.SystemRole, s.CreatedAt });
    return Results.Ok(dtos);
})
.WithName("GetStaff")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapPost("/api/staff", async (RegisterStaffDto request, StaffService staffService, TransactionHistoryService txnService) =>
{
    var error = await staffService.RegisterStaffAsync(request.Email, request.Password, request.Name, request.Role);
    if (error != null) return Results.BadRequest(new { error });
    
    await txnService.LogManagerActionAsync("SYSTEM", "StaffCreated", $"Created staff account for {request.Name}");
    return Results.Ok();
})
.WithName("CreateStaff")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapPut("/api/staff/{id}/role", async (Guid id, UpdateRoleDto request, StaffService staffService, TransactionHistoryService txnService) =>
{
    var success = await staffService.ChangeRoleAsync(id, request.Role);
    if (!success) return Results.BadRequest(new { error = "Failed to update role." });

    await txnService.LogManagerActionAsync("SYSTEM", "RoleUpdated", $"Updated role to {request.Role} for user {id}");
    return Results.Ok();
})
.WithName("UpdateStaffRole")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapDelete("/api/staff/{id}", async (Guid id, StaffService staffService, TransactionHistoryService txnService) =>
{
    var success = await staffService.DeleteStaffAsync(id);
    if (!success) return Results.BadRequest(new { error = "Failed to delete user." });

    await txnService.LogManagerActionAsync("SYSTEM", "StaffDeleted", $"Deleted staff account {id}");
    return Results.Ok();
})
.WithName("DeleteStaff")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapPost("/api/audit/log", async (AuditLogDto request, TransactionHistoryService txnService) =>
{
    await txnService.LogManagerActionAsync(request.ManagerName, request.Action, request.Details, request.StaffId);
    return Results.Ok("Audit log recorded.");
})
.WithName("PostAuditLog")
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
.WithOpenApi();

// DEV-52: Staff Management Endpoints (Admin only)

app.MapGet("/api/staff", async (Supabase.Client client) =>
{
    try
    {
        var users = await client.From<UserContext>().Get();
        var dtos = users.Models.Select(u => new
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
.WithName("GetAllStaff")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapPost("/api/staff", async (CreateStaffDto request, Supabase.Client client, IHttpClientFactory httpClientFactory, IConfiguration config) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.Name))
        return Results.BadRequest(new { error = "Email, password, and name are required." });

    var validRoles = new[] { "Admin", "Staff", "Technician" };
    if (!validRoles.Contains(request.Role))
        return Results.BadRequest(new { error = "Role must be Admin, Staff, or Technician." });

    try
    {
        // DEV-52: Use the Supabase Admin API instead of client.Auth.SignUp().
        // SignUp() mutates the shared singleton Supabase.Client's auth session (replacing the
        // service-role session with the new user's session), breaking all subsequent DB calls.
        // The Admin API creates the user server-side, confirms email automatically so the
        // account is immediately usable, and leaves the singleton's session untouched.
        var supabaseUrl = config["Supabase:Url"]!;
        var serviceRoleKey = config["Supabase:Key"]!;

        var http = httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", serviceRoleKey);
        http.DefaultRequestHeaders.Add("apikey", serviceRoleKey);

        var adminPayload = new
        {
            email = request.Email,
            password = request.Password,
            email_confirm = true   // skip email verification — manager-issued accounts are immediately active
        };

        var adminResponse = await http.PostAsJsonAsync($"{supabaseUrl}/auth/v1/admin/users", adminPayload);

        if (!adminResponse.IsSuccessStatusCode)
        {
            var errBody = await adminResponse.Content.ReadAsStringAsync();
            return Results.Json(new { error = "Auth error: " + errBody }, statusCode: (int)adminResponse.StatusCode);
        }

        var authUser = await adminResponse.Content.ReadFromJsonAsync<SupabaseAdminUserResponse>();
        if (string.IsNullOrWhiteSpace(authUser?.Id))
            return Results.Json(new { error = "Auth user created but ID was missing in the response." }, statusCode: 500);

        // Insert profile row into the Users table (service-role client — RLS bypassed)
        var newUser = new UserContext
        {
            Id = Guid.Parse(authUser.Id),
            Name = request.Name,
            Email = request.Email,
            SystemRole = request.Role
        };
        await client.From<UserContext>().Insert(newUser);

        // DEV-17: Audit log — Staff created
        try
        {
            var txnService = app.Services.GetRequiredService<TransactionHistoryService>();
            await txnService.LogManagerActionAsync("Admin", "STAFF_CREATED", $"Created {request.Role} account: {request.Name} ({request.Email})");
        }
        catch { /* best effort */ }

        return Results.Ok(new { id = authUser.Id, name = request.Name, email = request.Email, role = request.Role });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
})
.WithName("CreateStaff")
.RequireAuthorization("AdminOnly")
.WithOpenApi();

app.MapPut("/api/staff/{id}/role", async (Guid id, UpdateRoleDto request, Supabase.Client client) =>
{
    var validRoles = new[] { "Admin", "Staff", "Technician" };
    if (!validRoles.Contains(request.Role))
        return Results.BadRequest(new { error = "Role must be Admin, Staff, or Technician." });

    try
    {
        await client.From<UserContext>()
            .Where(u => u.Id == id)
            .Set(u => u.SystemRole, request.Role)
            .Update();

        // DEV-17: Audit log — Role changed
        try
        {
            var txnService = app.Services.GetRequiredService<TransactionHistoryService>();
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

app.MapDelete("/api/staff/{id}", async (Guid id, Supabase.Client client, IHttpClientFactory httpClientFactory, IConfiguration config) =>
{
    try
    {
        // Step 1: Delete the profile row from the Users table
        await client.From<UserContext>()
            .Where(u => u.Id == id)
            .Delete();

        // Step 2: Delete the auth user via Admin API so the account cannot be used to log in.
        // (The Supabase.Client SDK does not expose admin user deletion — must call REST directly.)
        var supabaseUrl = config["Supabase:Url"]!;
        var serviceRoleKey = config["Supabase:Key"]!;

        var http = httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", serviceRoleKey);
        http.DefaultRequestHeaders.Add("apikey", serviceRoleKey);

        await http.DeleteAsync($"{supabaseUrl}/auth/v1/admin/users/{id}");
        // Non-fatal: if the auth user was already gone, the profile is already removed above.

        // DEV-17: Audit log — Staff deleted
        try
        {
            var txnService = app.Services.GetRequiredService<TransactionHistoryService>();
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
app.MapGet("/api/analytics/revpamh", async (DateTime? from, DateTime? to, AnalyticsService analyticsService) =>
{
    var fromDate = from ?? DateTime.UtcNow.Date.AddDays(-30);
    var toDate = to ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);
    var result = await analyticsService.GetRevPAMHAsync(fromDate, toDate);
    return Results.Ok(result);
})
.WithName("GetRevPAMH")
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

// DEV-52: Minimal projection of the Supabase Admin API user-creation response
public class SupabaseAdminUserResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string? Id { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("email")]
    public string? Email { get; set; }
}
