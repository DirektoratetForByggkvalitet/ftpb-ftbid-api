using Ftpb.Ftbid.Domain.Referansedata;

namespace Ftpb.Ftbid.Domain.Referansedata;

/// <summary>Lokale referansedata fra adapterens skjemametadata og søkeindeks.</summary>
public sealed class Skjemametadata
{
    public string Arkivreferanse { get; set; } = "";
    public string? Skjematype { get; set; }
    public List<string> Tiltakstyper { get; set; } = [];
}
