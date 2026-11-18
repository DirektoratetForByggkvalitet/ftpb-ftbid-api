namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>Ingen ftbider samsvarer med søkefiltrene.</summary>
public sealed class SøkeresultatIkkeFunnetException()
    : FtbidException("Ingen ftbider funnet med de oppgitte søkekriteriene.", Feilkode.V0072, FeilType.Feil);
