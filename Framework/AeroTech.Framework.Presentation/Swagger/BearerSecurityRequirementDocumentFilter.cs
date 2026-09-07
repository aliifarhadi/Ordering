using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AeroTech.Framework.Presentation.Swagger
{
    public sealed class BearerSecurityRequirementDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            swaggerDoc.Security = new List<OpenApiSecurityRequirement>
            {
                new()
                {
                    { new OpenApiSecuritySchemeReference("Bearer", swaggerDoc), new List<string>() }
                }
            };
        }
    }
}
