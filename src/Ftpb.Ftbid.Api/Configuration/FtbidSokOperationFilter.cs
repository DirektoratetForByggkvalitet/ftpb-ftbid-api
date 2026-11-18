using Ftpb.Ftbid.Api.Controllers;
using Ftpb.Ftbid.Application.Contracts;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Ftpb.Ftbid.Api.Configuration;

/// <summary>Beskriver begge svarformatene for listing og filtrert søk på samme endepunkt.</summary>
public sealed class FtbidSokOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(FtbidController) ||
            context.MethodInfo.Name != nameof(FtbidController.SearchAsync)) return;

        operation.Responses!["200"].Content!["application/json"].Schema = new OpenApiSchema
        {
            OneOf =
            [
                context.SchemaGenerator.GenerateSchema(typeof(PagedResult<FtbidResult>), context.SchemaRepository),
                context.SchemaGenerator.GenerateSchema(typeof(List<FtbidResult>), context.SchemaRepository)
            ]
        };
    }
}
