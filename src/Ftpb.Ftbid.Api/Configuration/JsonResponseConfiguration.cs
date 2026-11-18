using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace Ftpb.Ftbid.Api.Configuration;

/// <summary>Felles serialisering for kontrollere og HTTP-mellomvare.</summary>
public static class JsonResponseConfiguration
{
    public static void IncludeAllValues(JsonSerializerOptions options)
    {
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        options.TypeInfoResolver = (options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver()).WithAddedModifier(type =>
        {
            // ProblemDetails har egne attributter som ellers utelater null uavhengig av standardoppsettet.
            if (!typeof(ProblemDetails).IsAssignableFrom(type.Type)) return;
            foreach (var field in type.Properties)
                field.ShouldSerialize = (_, _) => true;
        });
    }
}
