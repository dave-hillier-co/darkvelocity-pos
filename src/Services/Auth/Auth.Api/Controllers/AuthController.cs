using DarkVelocity.Auth.Api.Dtos;
using DarkVelocity.Auth.Api.Services;
using DarkVelocity.Shared.Contracts.Events;
using DarkVelocity.Shared.Infrastructure.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Auth.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IEventBus _eventBus;

    public AuthController(IAuthService authService, IEventBus eventBus)
    {
        _authService = authService;
        _eventBus = eventBus;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginWithPinAsync(request.Pin, request.LocationId);

        if (result == null)
        {
            // Publish login failed event
            await _eventBus.PublishAsync(new LoginFailed(
                UserId: null,
                LocationId: request.LocationId,
                LoginMethod: "pin",
                FailureReason: "Invalid PIN",
                IpAddress: GetClientIpAddress(),
                AttemptedIdentifier: null
            ));

            return Unauthorized(new { message = "Invalid PIN" });
        }

        // Publish successful login event
        await _eventBus.PublishAsync(new UserLoggedIn(
            UserId: result.User.Id,
            TenantId: Guid.Empty, // Will be resolved from location
            LocationId: request.LocationId ?? result.User.HomeLocationId,
            Username: result.User.Username,
            LoginMethod: "pin",
            IpAddress: GetClientIpAddress(),
            UserAgent: GetUserAgent()
        ));

        return Ok(result);
    }

    [HttpPost("login/qr")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> LoginWithQr([FromBody] QrLoginRequest request)
    {
        var result = await _authService.LoginWithQrAsync(request.Token, request.LocationId);

        if (result == null)
        {
            // Publish login failed event
            await _eventBus.PublishAsync(new LoginFailed(
                UserId: null,
                LocationId: request.LocationId,
                LoginMethod: "qr",
                FailureReason: "Invalid QR token",
                IpAddress: GetClientIpAddress(),
                AttemptedIdentifier: request.Token?.Substring(0, Math.Min(8, request.Token?.Length ?? 0)) + "..."
            ));

            return Unauthorized(new { message = "Invalid QR token" });
        }

        // Publish successful login event
        await _eventBus.PublishAsync(new UserLoggedIn(
            UserId: result.User.Id,
            TenantId: Guid.Empty,
            LocationId: request.LocationId ?? result.User.HomeLocationId,
            Username: result.User.Username,
            LoginMethod: "qr",
            IpAddress: GetClientIpAddress(),
            UserAgent: GetUserAgent()
        ));

        return Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request.RefreshToken);

        if (result == null)
            return Unauthorized(new { message = "Invalid refresh token" });

        // Publish token refreshed event
        await _eventBus.PublishAsync(new TokenRefreshed(
            UserId: result.User.Id,
            TenantId: Guid.Empty,
            LocationId: result.User.HomeLocationId
        ));

        return Ok(result);
    }

    private string? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent()
    {
        return Request.Headers.UserAgent.ToString();
    }
}
