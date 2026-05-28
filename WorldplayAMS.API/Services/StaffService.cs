using Microsoft.Extensions.Logging;
using Supabase.Gotrue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WorldplayAMS.Core.Models;

namespace WorldplayAMS.API.Services;

public class StaffService
{
    private readonly Supabase.Client _client;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<StaffService> _logger;

    public StaffService(Supabase.Client client, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<StaffService> logger)
    {
        _client = client;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task<List<UserContext>> GetAllStaffAsync()
    {
        try
        {
            var response = await _client.From<UserContext>().Get();
            return response.Models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get staff list");
            return new List<UserContext>();
        }
    }

    public class StaffRegistrationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? AuthUserId { get; set; }
    }

    public async Task<StaffRegistrationResult> RegisterStaffAsync(string email, string password, string name, string role)
    {
        try
        {
            var existing = await _client.From<UserContext>().Where(x => x.Email == email).Get();
            if (existing.Models.Any())
            {
                return new StaffRegistrationResult { Success = false, ErrorMessage = "A user with this email already exists." };
            }

            var supabaseUrl = _config["Supabase:Url"]!;
            var serviceRoleKey = _config["Supabase:Key"]!;

            var http = _httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", serviceRoleKey);
            http.DefaultRequestHeaders.Add("apikey", serviceRoleKey);

            var adminPayload = new
            {
                email = email,
                password = password,
                email_confirm = true
            };

            var adminResponse = await http.PostAsJsonAsync($"{supabaseUrl}/auth/v1/admin/users", adminPayload);

            if (!adminResponse.IsSuccessStatusCode)
            {
                var errBody = await adminResponse.Content.ReadAsStringAsync();
                return new StaffRegistrationResult { Success = false, ErrorMessage = "Auth error: " + errBody };
            }

            var authUser = await adminResponse.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>();
            var authUserId = authUser?["id"]?.ToString();
            
            if (string.IsNullOrWhiteSpace(authUserId))
            {
                return new StaffRegistrationResult { Success = false, ErrorMessage = "Auth user created but ID was missing in the response." };
            }

            var userContext = new UserContext
            {
                Id = Guid.Parse(authUserId),
                Email = email,
                Name = name,
                SystemRole = role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _client.From<UserContext>().Insert(userContext);

            return new StaffRegistrationResult { Success = true, AuthUserId = authUserId };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering staff.");
            return new StaffRegistrationResult { Success = false, ErrorMessage = "Internal server error during registration." };
        }
    }

    public async Task<bool> ChangeRoleAsync(Guid userId, string newRole)
    {
        try
        {
            var update = await _client.From<UserContext>()
                .Where(x => x.Id == userId)
                .Set(x => x.SystemRole, newRole)
                .Set(x => x.UpdatedAt, DateTime.UtcNow)
                .Update();
            
            return update.Models.Any();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing staff role.");
            return false;
        }
    }

    public async Task<string?> DeleteStaffAsync(Guid userId)
    {
        try
        {
            await _client.From<UserContext>().Where(x => x.Id == userId).Delete();

            var supabaseUrl = _config["Supabase:Url"]!;
            var serviceRoleKey = _config["Supabase:Key"]!;

            var http = _httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", serviceRoleKey);
            http.DefaultRequestHeaders.Add("apikey", serviceRoleKey);

            await http.DeleteAsync($"{supabaseUrl}/auth/v1/admin/users/{userId}");
            
            return null; // Success
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting staff.");
            return ex.Message;
        }
    }
}
