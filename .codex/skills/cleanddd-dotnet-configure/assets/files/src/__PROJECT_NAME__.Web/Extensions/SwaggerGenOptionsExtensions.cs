using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace __PROJECT_NAME__.Web.Extensions;

public static class SwaggerGenOptionsExtensions
{
    public static SwaggerGenOptions AddEntityIdSchemaMap(this SwaggerGenOptions swaggerGenOptions)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                     .Where(p => p.FullName != null && p.FullName.Contains("__PROJECT_NAME__")))
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsClass && Array.Exists(type.GetInterfaces(), p => p == typeof(IEntityId)))
                {
                    swaggerGenOptions.MapType(type,
                        () => new OpenApiSchema { Type = typeof(string).Name.ToLower() });
                }
            }
        }

        return swaggerGenOptions;
    }
}