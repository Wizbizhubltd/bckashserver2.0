using BCKash.SharedKernel;

namespace BCKash.Api.Infrastructure;

/// <summary>
/// Reads "who is calling" from the current request's JWT claims (set by
/// JwtTokenService.GenerateAccessToken). Scoped per-request — registered as Scoped
/// in DI so the audit interceptor and permission checks see a consistent caller for
/// the whole request.
/// </summary>
public class CurrentHttpUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentHttpUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public long? OfficeId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirst("office_id")?.Value;
            return long.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
