using Ftpb.Ftbid.Api.Kontrollere;
using Ftpb.Ftbid.Application.Kontrakter;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Ftpb.Ftbid.Api.Konfigurasjon;

/// <summary>Beskriver begge svarformatene for listing og filtrert søk på samme endepunkt.</summary>
public sealed class FtbidSokOperasjonsfilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(FtbidKontroller) ||
            context.MethodInfo.Name != nameof(FtbidKontroller.SokAsync)) return;

        operation.Responses!["200"].Content!["application/json"].Schema = new OpenApiSchema
        {
            OneOf =
            [
                context.SchemaGenerator.GenerateSchema(typeof(PaginertResultat<FtbidResultat>), context.SchemaRepository),
                context.SchemaGenerator.GenerateSchema(typeof(List<FtbidResultat>), context.SchemaRepository)
            ]
        };
    }
}
