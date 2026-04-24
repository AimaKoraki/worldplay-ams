using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Xunit;
using WorldplayAMS.UI.Components.Pages;

namespace WorldplayAMS.Tests.UI
{
    public class RfidScannerTests : BunitContext
    {
        [Fact]
        public void RfidScanner_DisplaysCalculatedLKR_OnCheckout()
        {
            // Arrange
            var mockMessageHandler = new Mock<HttpMessageHandler>();
            mockMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.PathAndQuery.Contains("/api/sessions/process-tap")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("\"Success: Checked out. Duration: 45 min | Fee: LKR 6.75\"") // Raw JSON string
                });

            var httpClient = new HttpClient(mockMessageHandler.Object)
            {
                BaseAddress = new System.Uri("http://localhost:5089")
            };

            var mockClientFactory = new Mock<IHttpClientFactory>();
            mockClientFactory.Setup(cf => cf.CreateClient("ApiClient")).Returns(httpClient);

            Services.AddSingleton<IHttpClientFactory>(mockClientFactory.Object);

            // Register a fake AuthenticationStateProvider with a Staff identity
            var staffIdentity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "Test Staff"),
                new Claim(ClaimTypes.Role, "Staff")
            }, "TestAuth");
            var staffPrincipal = new ClaimsPrincipal(staffIdentity);
            var fakeAuthState = Task.FromResult(new AuthenticationState(staffPrincipal));

            var mockAuthProvider = new Mock<AuthenticationStateProvider>();
            mockAuthProvider.Setup(a => a.GetAuthenticationStateAsync()).Returns(fakeAuthState);
            Services.AddSingleton<AuthenticationStateProvider>(mockAuthProvider.Object);

            // Act
            var component = Render<RfidScanner>();
            
            // Wait for input to be attached, it is an <input type="text" @bind="scannedTag" @onkeyup="HandleKeyUp" />
            var testInput = component.Find("input[type='text']");
            testInput.Input("DEMO-TAG-001");
            testInput.KeyUp(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

            // Assert
            // The process tap updates the UI asynchronously, bUnit gives us a wait
            component.WaitForState(() => component.Markup.Contains("LKR 6.75"));

            component.Markup.Should().Contain("LKR 6.75", "Because the parsing logic should properly extract the LKR amount from the API string and render it into the checkout billing display.");
            component.Markup.Should().Contain("45", "Because it displays duration.");
        }
    }
}
