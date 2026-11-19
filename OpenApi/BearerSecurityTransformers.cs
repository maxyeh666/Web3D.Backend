using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Web3D.Backend.OpenApi
{
    // 負責在 OpenApi 文件中註冊 JWT Bearer 驗證方式
    internal sealed class BearerSecuritySchemeTransformer (
        IAuthenticationSchemeProvider authenticationSchemeProvider
    ) : IOpenApiDocumentTransformer
    {
        public async Task TransformAsync(
            OpenApiDocument document,
            OpenApiDocumentTransformerContext context,
            CancellationToken cancellationToken
        )
        {
            var schemes = await authenticationSchemeProvider.GetAllSchemesAsync();

            if (!schemes.Any(scheme => scheme.Name == "Bearer"))
            {
                return;
            }

            document.Components ??= new OpenApiComponents();

            document.Components.SecuritySchemes = 
            new Dictionary<string, IOpenApiSecurityScheme>
            {
                ["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                }
            };
        }
    }
    // 負責判斷個別 API 是否需要 JWT
    internal sealed class AuthorizeOperationTransformer : IOpenApiOperationTransformer
    {
        public Task TransformAsync(
            OpenApiOperation operation,
            OpenApiOperationTransformerContext context,
            CancellationToken cancellationToken
        )
        {
            // 從 API 的 Endpoint Metadata 找出 [Authorize]
            var requiresAuthorization = 
                context.Description
                .ActionDescriptor
                .EndpointMetadata
                .OfType<IAuthorizeData>()
                .Any();

            // 沒有 [Authorize] 就不需要 JWT
            if (!requiresAuthorization)
            {
                return Task.CompletedTask;
            }

            operation.Security ??= new List<OpenApiSecurityRequirement>();

            operation.Security.Add(
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(
                        "Bearer",
                        context.Document
                    )] = new List<string>()
                }
            );

            return Task.CompletedTask;
        }
    }
}