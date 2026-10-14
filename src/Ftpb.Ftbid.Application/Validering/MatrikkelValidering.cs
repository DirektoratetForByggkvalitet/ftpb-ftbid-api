using Ftpb.Ftbid.Application.Kontrakter;
using Ftpb.Ftbid.Application.Lagring;
using Ftpb.Ftbid.Application.Services;
using Ftpb.Ftbid.Domain.Feil.Unntak;

namespace Ftpb.Ftbid.Application.Validering;

/// <summary>Felles format- og gyldighetskontroll av matrikkelenheter.</summary>
internal static class MatrikkelValidering
{
    internal static Matrikkeldata Normaliser(Matrikkeldata? matrikkel)
    {
        if (matrikkel is null || matrikkel.Kommunenummer is null)
            throw new UgyldigeMatrikkelopplysningerException();
        var kommune = matrikkel.Kommunenummer.Trim();
        if (kommune.Length != 4 || !kommune.All(char.IsAsciiDigit))
            throw new UgyldigKommunenummerException(kommune);
        if (matrikkel.Gaardsnummer < 0 || matrikkel.Bruksnummer < 0 ||
            matrikkel.Festenummer < 0 || matrikkel.Seksjonsnummer < 0)
            throw new UgyldigeMatrikkelopplysningerException();
        return matrikkel with { Kommunenummer = kommune };
    }

    internal static async Task<Matrikkeldata> ValiderNyAsync(Matrikkeldata matrikkel,
        IKommuneregister kommuneregister, IMatrikkelService matrikkelservice, CancellationToken avbrudd)
    {
        var normalisert = Normaliser(matrikkel);
        if (!await kommuneregister.ErKommuneGyldigAsync(normalisert.Kommunenummer, DateTimeOffset.UtcNow, avbrudd))
            throw new UgyldigKommunenummerException(normalisert.Kommunenummer);
        await matrikkelservice.ValiderAsync(normalisert, avbrudd);
        return normalisert;
    }
}
