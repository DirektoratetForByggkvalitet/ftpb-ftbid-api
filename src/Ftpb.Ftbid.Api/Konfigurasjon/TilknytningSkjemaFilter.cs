using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Validering;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Ftpb.Ftbid.Api.Konfigurasjon;

/// <summary>Dokumenterer URN-kravet for nye tilknytninger og historiske verdier i svar.</summary>
public sealed class TilknytningSkjemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema objekt) return;
        if (context.Type != typeof(TilknyttFtbid) && context.Type != typeof(Tilknytning)) return;
        if (schema.Properties?["ReferanseId"] is not OpenApiSchema referanse) return;
        referanse.MaxLength = 80;
        if (context.Type == typeof(TilknyttFtbid))
        {
            objekt.Required ??= new HashSet<string>();
            objekt.Required.Add("ReferanseId");
            referanse.Type = JsonSchemaType.String;
            referanse.Pattern = UrnValidering.Monster;
            referanse.Description = "Gyldig URN etter RFC 8141 (syntaks), maksimalt 80 tegn. Eksempel: urn:dibk:bygg:innsending-id:65465411-34184-13213. Ugyldig URN gir 400 med feilkode V0073. Referansen må være registrert i metadata før tilknytning.";
        }
        else
            referanse.Description = "Referanse til skjema, rapport eller annen ressurs. Eldre referanser returneres uendret og kan mangle URN-format.";
    }
}
