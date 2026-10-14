using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Identifikatoren finnes ikke ved tilknytning eller endring.</summary>
public sealed class FtbidIkkeFunnetException(string ftbid, Exception? indreUnntak = null)
    : ValideringsException($"Ftbid {ftbid} eksisterer ikke.", Feilkode.V0054, indreUnntak)
{
    public string Ftbid { get; } = ftbid;
}
