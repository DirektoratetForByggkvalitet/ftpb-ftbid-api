namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Ingen ftbider samsvarer med søkefiltrene.</summary>
public sealed class SøkeresultatIkkeFunnetException()
    : FtbidException("Ingen ftbider funnet med de oppgitte søkekriteriene.", Feilkode.V0072, FeilType.Feil);
