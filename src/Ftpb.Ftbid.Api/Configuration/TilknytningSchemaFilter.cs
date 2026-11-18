using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Application.Validation;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Ftpb.Ftbid.Api.Configuration;

/// <summary>Dokumenterer URN-kravet for nye tilknytninger og historiske verdier i svar.</summary>
public sealed class TilknytningSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema objekt) return;
        if (context.Type == typeof(TilknyttFtbidRequest))
        {
            objekt.Required ??= new HashSet<string>();
            objekt.Required.Add("Tilknytninger");
            if (schema.Properties?["Tilknytninger"] is OpenApiSchema liste)
            {
                liste.Type = JsonSchemaType.Array;
                liste.MinItems = 1;
            }
            return;
        }
        if (context.Type != typeof(TilknytningRequest) && context.Type != typeof(Tilknytning)) return;
        if (schema.Properties?["ReferanseId"] is not OpenApiSchema referanse) return;
        referanse.MaxLength = 80;
        if (context.Type == typeof(TilknytningRequest))
        {
            objekt.Required ??= new HashSet<string>();
            objekt.Required.Add("ReferanseId");
            referanse.Type = JsonSchemaType.String;
            referanse.Pattern = UrnValidation.Pattern;
            referanse.Description = "Gyldig URN etter RFC 8141 (syntaks), maksimalt 80 tegn. Eksempel: urn:dibk:bygg:innsending-id:65465411-34184-13213. Ugyldig URN gir 400 med feilkode V0073. Metadata registreres eller oppdateres ved tilknytning.";
        }
        else
            referanse.Description = "Referanse til skjema, rapport eller annen ressurs. Eldre referanser returneres uendret og kan mangle URN-format.";
    }
}
