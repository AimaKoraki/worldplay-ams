using System;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WorldplayAMS.API.Services;
using WorldplayAMS.Core.Models;
using Xunit;

namespace WorldplayAMS.Tests.Services;

public class StaffServiceTests : IAsyncLifetime
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<StaffService>> _mockLogger;
    private readonly IConfiguration _configuration;
    private Supabase.Client _supabaseClient = null!;
    private StaffService _sut = null!;

    public StaffServiceTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<StaffService>>();

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Supabase:Url", "http://localhost:0" },
                { "Supabase:Key", "fake-key" }
            })
            .Build();

        // Return a real HttpClient so the factory doesn't throw on its own
        _mockHttpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient());
    }

    public async Task InitializeAsync()
    {
        // Arrange — create a Supabase client pointed at a non-existent server
        var options = new Supabase.SupabaseOptions
        {
            AutoRefreshToken = false,
            AutoConnectRealtime = false
        };
        _supabaseClient = new Supabase.Client("http://localhost:0", "fake-key", options);
        await _supabaseClient.InitializeAsync();

        _sut = new StaffService(_supabaseClient, _mockHttpClientFactory.Object, _configuration, _mockLogger.Object);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetAllStaffAsync_ReturnsEmptyList_WhenSupabaseConnectionFails()
    {
        // Act
        var result = await _sut.GetAllStaffAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ChangeRoleAsync_ReturnsFalse_WhenSupabaseConnectionFails()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await _sut.ChangeRoleAsync(userId, "Admin");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteStaffAsync_ReturnsErrorMessage_WhenSupabaseConnectionFails()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await _sut.DeleteStaffAsync(userId);

        // Assert
        result.Should().NotBeNullOrEmpty("because a failed delete should return the exception message");
    }
}
