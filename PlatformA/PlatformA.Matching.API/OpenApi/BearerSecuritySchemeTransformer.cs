using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PlatformA.Matching.API.OpenApi;

internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT Access Token을 입력하세요. 예: eyJhbGci..."
        };
        return Task.CompletedTask;
    }
}
