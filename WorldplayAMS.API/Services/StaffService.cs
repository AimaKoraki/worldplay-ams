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
    private readonly ILogger<StaffService> _logger;

    public StaffService(Supabase.Client client, ILogger<StaffService> logger)
    {
        _client = client;
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

    public async Task<string?> RegisterStaffAsync(string email, string password, string name, string role)
    {
        try
        {
            // First, check if the user context already exists
            var existing = await _client.From<UserContext>().Where(x => x.Email == email).Get();
            if (existing.Models.Any())
            {
                return "A user with this email already exists.";
            }

            // Create user in Supabase Auth
            var session = await _client.Auth.SignUp(email, password);
            var authUserId = session?.User?.Id;
            
            if (string.IsNullOrEmpty(authUserId))
            {
                return "Failed to create authentication record.";
            }

            // Create user context
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

            return null; // success
        }
        catch (Supabase.Gotrue.Exceptions.GotrueException ex)
        {
            _logger.LogError(ex, "Gotrue error during staff registration.");
            return ex.Reason.ToString() ?? "Authentication provider rejected the request.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering staff.");
            return "Internal server error during registration.";
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

    public async Task<bool> DeleteStaffAsync(Guid userId)
    {
        try
        {
            // Note: In Supabase, deleting from UserContext does not automatically delete from Auth schema unless there's a trigger.
            // But for the sake of the AMS access control, deleting their UserContext completely revokes their access to the system.
            await _client.From<UserContext>()
                .Where(x => x.Id == userId)
                .Delete();
                
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting staff.");
            return false;
        }
    }
}
