using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace Ftpb.Ftbid.Api.Konfigurasjon;

/// <summary>Felles serialisering for kontrollere og HTTP-mellomvare.</summary>
public static class JsonResponsoppsett
{
    public static void InkluderAlleVerdier(JsonSerializerOptions valg)
    {
        valg.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        valg.TypeInfoResolver = (valg.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver()).WithAddedModifier(type =>
        {
            // ProblemDetails har egne attributter som ellers utelater null uavhengig av standardoppsettet.
            if (!typeof(ProblemDetails).IsAssignableFrom(type.Type)) return;
            foreach (var felt in type.Properties)
                felt.ShouldSerialize = (_, _) => true;
        });
    }
}
