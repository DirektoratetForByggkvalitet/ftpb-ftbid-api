using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Matrikkel;
using Ftpb.Ftbid.Domain.Feil.Unntak;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Sikrer at nye matrikkeltilknytninger finnes i Kartverkets Matrikkel.</summary>
public sealed class MatrikkelService(IMatrikkelKlient klient) : IMatrikkelService
{
    /// <inheritdoc />
    public async Task ValiderAsync(Matrikkeldata matrikkel, CancellationToken avbrudd = default)
    {
        var identifikatorer = await klient.HentMatrikkelenhetIderAsync(matrikkel, avbrudd);
        if (identifikatorer.Count == 0)
            throw new MatrikkelenhetIkkeFunnetException(
                $"{matrikkel.Kommunenummer}-{matrikkel.Gaardsnummer}/{matrikkel.Bruksnummer}/{matrikkel.Festenummer}/{matrikkel.Seksjonsnummer}");
    }
}
