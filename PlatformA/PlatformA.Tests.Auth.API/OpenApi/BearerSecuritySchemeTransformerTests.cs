using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using PlatformA.Auth.API.OpenApi;
using Xunit;

namespace PlatformA.Tests.Auth.API.OpenApi
{
    public class BearerSecuritySchemeTransformerTests
    {
        private readonly BearerSecuritySchemeTransformer _transformer = new();

        private static OpenApiDocument CreateDocument() => new()
        {
            Components = new OpenApiComponents
            {
                SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>()
            }
        };

        [Fact]
        public async Task TransformAsync_WhenComponentsIsNull_InitializesAndAddsBearer()
        {
            var document = new OpenApiDocument { Components = null };

            await _transformer.TransformAsync(document, null!, CancellationToken.None);

            Assert.NotNull(document.Components);
            Assert.True(document.Components.SecuritySchemes!.ContainsKey("Bearer"));
        }

        [Fact]
        public async Task TransformAsync_AddsBearer_WithCorrectSchemeProperties()
        {
            var document = CreateDocument();

            await _transformer.TransformAsync(document, null!, CancellationToken.None);

            var scheme = (OpenApiSecurityScheme)document.Components!.SecuritySchemes!["Bearer"];
            Assert.Equal(SecuritySchemeType.Http, scheme.Type);
            Assert.Equal("bearer", scheme.Scheme);
            Assert.Equal("JWT", scheme.BearerFormat);
        }

        [Fact]
        public async Task TransformAsync_WhenBearerAlreadyExists_Overwrites()
        {
            var document = new OpenApiDocument
            {
                Components = new OpenApiComponents
                {
                    SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                    {
                        ["Bearer"] = new OpenApiSecurityScheme
                        {
                            Type = SecuritySchemeType.ApiKey,
                            Scheme = "old-scheme"
                        }
                    }
                }
            };

            await _transformer.TransformAsync(document, null!, CancellationToken.None);

            var scheme = (OpenApiSecurityScheme)document.Components.SecuritySchemes!["Bearer"];
            Assert.Equal(SecuritySchemeType.Http, scheme.Type);
            Assert.Equal("bearer", scheme.Scheme);
        }

        [Fact]
        public async Task TransformAsync_ReturnsCompletedTask()
        {
            var document = CreateDocument();

            var result = _transformer.TransformAsync(document, null!, CancellationToken.None);

            Assert.True(result.IsCompletedSuccessfully);
        }
    }
}
