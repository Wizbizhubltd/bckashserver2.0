using System.Security.Claims;
using BCKash.Application.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace BCKash.Api.Infrastructure;

/// <summary>
/// Per-request checks layered on top of plain JWT validation:
/// only the user's current sign-in session is accepted (one device at a time), and a user with a
/// temporary password can do nothing but change it. Both reject with a machine-readable
/// <c>reason</c> in the problem body so the portals can show the right message.
/// </summary>
public static class SessionEnforcement
{
    public const string SessionReplacedReason = "session_replaced";
    public const string PasswordChangeRequiredReason = "password_change_required";

    private const string SessionReplacedItemKey = "BCKash.SessionReplaced";

    // Calls a user with a temporary password may still make.
    private static readonly string[] PasswordChangeAllowedPaths = ["/api/v1/auth/password/change", "/api/v1/users/me"];

    /// <summary>JwtBearerEvents.OnTokenValidated: rejects tokens from a session that has since been replaced by a newer sign-in.</summary>
    public static async Task RejectReplacedSessionAsync(TokenValidatedContext context)
    {
        var principal = context.Principal!;
        var sessionId = principal.FindFirstValue(AuthClaimTypes.SessionId);
        var validUserId = int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);

        var checker = context.HttpContext.RequestServices.GetRequiredService<IActiveSessionChecker>();
        if (!validUserId || sessionId is null || !await checker.IsActiveAsync(userId, sessionId, context.HttpContext.RequestAborted))
        {
            context.HttpContext.Items[SessionReplacedItemKey] = true;
            context.Fail("This session has ended because the account signed in on another device.");
        }
    }

    /// <summary>JwtBearerEvents.OnChallenge: explains a replaced-session 401 instead of the bare default challenge.</summary>
    public static async Task ExplainReplacedSessionAsync(JwtBearerChallengeContext context)
    {
        if (!context.HttpContext.Items.ContainsKey(SessionReplacedItemKey))
        {
            return;
        }

        context.HandleResponse();
        await Results.Problem(
                title: "You were signed out because your account was signed in on another device.",
                statusCode: StatusCodes.Status401Unauthorized,
                extensions: new Dictionary<string, object?> { ["reason"] = SessionReplacedReason })
            .ExecuteAsync(context.HttpContext);
    }

    /// <summary>Middleware (after authentication): blocks everything but changing the password while a temporary password is in use.</summary>
    public static async Task RequirePasswordChangeFirstAsync(HttpContext context, RequestDelegate next)
    {
        var mustChange = context.User.HasClaim(AuthClaimTypes.PasswordChangeRequired, "true");
        var allowed = PasswordChangeAllowedPaths.Any(p => context.Request.Path.Equals(p, StringComparison.OrdinalIgnoreCase));

        if (mustChange && !allowed)
        {
            await Results.Problem(
                    title: "You must change your temporary password before continuing.",
                    statusCode: StatusCodes.Status403Forbidden,
                    extensions: new Dictionary<string, object?> { ["reason"] = PasswordChangeRequiredReason })
                .ExecuteAsync(context);
            return;
        }

        await next(context);
    }
}
