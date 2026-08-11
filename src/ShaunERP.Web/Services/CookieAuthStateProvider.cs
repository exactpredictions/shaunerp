using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using ShaunERP.Infrastructure.Platform;

namespace ShaunERP.Web.Services;

public class CookieAuthStateProvider : AuthenticationStateProvider
{
    private readonly IHttpContextAccessor _http;
    private readonly ITenantContext _tenant;
    private readonly IAuthorizationContextService _authz;

    public CookieAuthStateProvider(
        IHttpContextAccessor http,
        ITenantContext tenant,
        IAuthorizationContextService authz)
    {
        _http = http;
        _tenant = tenant;
        _authz = authz;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = _http.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
        // Hydrate tenant from cookie claims only — never touch DbContext here (Blazor concurrent render).
        if (user.Identity?.IsAuthenticated == true && _tenant.UserId is null)
        {
            _tenant.Set(_authz.BuildFromClaims(user));
        }

        return Task.FromResult(new AuthenticationState(user));
    }

    public void Notify() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
