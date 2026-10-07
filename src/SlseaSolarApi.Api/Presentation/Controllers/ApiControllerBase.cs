using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using SlseaSolarApi.Api.Application.Security;
using SlseaSolarApi.Api.Domain.Exceptions;

namespace SlseaSolarApi.Api.Presentation.Controllers;

/// <summary>
/// Shared plumbing for resource controllers: the claim-derived <see cref="CurrentUser"/>,
/// pagination link building from the real request URL, and ETag / If-None-Match conditional GET.
/// </summary>
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>The authenticated caller. Only available on endpoints that require authentication.</summary>
    protected new CurrentUser User
    {
        get
        {
            var principal = HttpContext.User;
            if (principal.Identity?.IsAuthenticated != true)
            {
                throw new UnauthorizedException();
            }

            return CurrentUser.FromClaimsPrincipal(principal);
        }
    }

    /// <summary>
    /// Builds an absolute URL for page <paramref name="page"/> by replacing the <c>page</c> query
    /// parameter of the current request, so next/prev links inherit every other filter and reflect
    /// the host the client actually used (correct behind the Render proxy).
    /// </summary>
    protected string BuildPageLink(int page)
    {
        var request = HttpContext.Request;
        var path = HttpContext.Request.PathBase + request.Path;

        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(
            request.QueryString.Value ?? string.Empty);

        query = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(query)
        {
            ["page"] = page.ToString()
        };

        var queryString = string.Join("&", query.Select(kv =>
            string.Join("&", kv.Value
                .Select(value => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(value ?? string.Empty)}"))));

        return $"{request.Scheme}://{request.Host}{path}?{queryString}";
    }

    private static readonly JsonSerializerOptions EtagJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Conditional GET (requirement 10). Computes a strong ETag over the response body, sets the
    /// <c>ETag</c> header, and returns a 304 without a body when the client's <c>If-None-Match</c>
    /// matches. Call this instead of <c>Ok(...)</c> for GET endpoints that support caching.
    /// </summary>
    /// <returns>An <c>IActionResult</c> the caller returns directly.</returns>
    protected IActionResult ConditionalOk<T>(T payload)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, EtagJsonOptions);
        var etag = $"\"{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(json))[..32]}\"";

        var ifNoneMatch = Request.Headers.IfNoneMatch.ToString();
        if (!string.IsNullOrEmpty(ifNoneMatch) &&
            ifNoneMatch.Split(',', StringSplitOptions.TrimEntries).Contains(etag, StringComparer.Ordinal))
        {
            // Not modified: headers only, no body (requirement 10).
            Response.StatusCode = StatusCodes.Status304NotModified;
            Response.Headers.ETag = etag;
            return new EmptyResult();
        }

        Response.Headers.ETag = etag;
        return Ok(payload);
    }
}
