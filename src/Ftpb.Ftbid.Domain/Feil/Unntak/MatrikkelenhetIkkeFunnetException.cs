using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Matrikkelnummeret finnes ikke i Kartverkets Matrikkel.</summary>
public sealed class MatrikkelenhetIkkeFunnetException(string matrikkelnummer, Exception? indreUnntak = null)
    : ValideringsException($"Matrikkelenheten {matrikkelnummer} finnes ikke i Matrikkelen.", Feilkode.V0063, indreUnntak)
{
    public string Matrikkelnummer { get; } = matrikkelnummer;
}
