using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace BCKash.Api.OpenApi;

/// <summary>
/// Registers the JWT bearer scheme with the OpenAPI document and requires it on every
/// operation, so Swagger UI's "Authorize" button can be used to call protected endpoints.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider)
    : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var schemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (!schemes.Any(scheme => scheme.Name == JwtBearerDefaults.AuthenticationScheme))
        {
            return;
        }

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT access token issued by POST /api/v1/auth/login.",
        };

        var bearerReference = new OpenApiSecuritySchemeReference("Bearer", document);
        var requirement = new OpenApiSecurityRequirement
        {
            [bearerReference] = [],
        };

        foreach (var pathItem in document.Paths.Values)
        {
            foreach (var operation in pathItem!.Operations!.Values)
            {
                operation!.Security ??= new List<OpenApiSecurityRequirement>();
                operation.Security.Add(requirement);
            }
        }
    }
}
