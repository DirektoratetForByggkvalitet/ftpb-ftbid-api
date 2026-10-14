using Ftpb.Ftbid.Domain.Referansedata;

namespace Ftpb.Ftbid.Domain.Prosjekter;

/// <summary>Knytter en eksisterende arkivreferanse til en ftbid, tidligere FormWorkflowLogs.</summary>
public sealed class Identifikasjonsmetadata
{
    public string Arkivreferanse { get; set; } = "";
    public string Ftbid { get; set; } = "";
    public Skjemametadata Skjemametadata { get; set; } = null!;
}
