using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Application.Repositories;
using Ftpb.Ftbid.Domain.Errors.Exceptions;

namespace Ftpb.Ftbid.Application.Validation;

/// <summary>Felles format- og gyldighetskontroll av matrikkelenheter.</summary>
internal static class MatrikkelValidation
{
    internal static Eiendomsidentifikasjon Normalize(Eiendomsidentifikasjon? matrikkel)
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

    internal static async Task<Eiendomsidentifikasjon> ValidateNewAsync(Eiendomsidentifikasjon matrikkel,
        IKommuneregister kommuneregister, CancellationToken cancellationToken)
    {
        var normalized = Normalize(matrikkel);
        if (!await kommuneregister.ErKommuneGyldigAsync(normalized.Kommunenummer, DateTimeOffset.UtcNow, cancellationToken))
            throw new UgyldigKommunenummerException(normalized.Kommunenummer);
        return normalized;
    }
}
