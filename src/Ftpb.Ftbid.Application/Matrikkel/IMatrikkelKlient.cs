using Ftpb.Ftbid.Application.Kontrakter;

namespace Ftpb.Ftbid.Application.Matrikkel;

/// <summary>Oppslag av eksakte matrikkelnummer hos Kartverket.</summary>
public interface IMatrikkelKlient
{
    /// <summary>Henter Kartverkets interne identifikatorer for alle fem feltene i matrikkelnummeret.</summary>
    Task<IReadOnlyList<long>> HentMatrikkelenhetIderAsync(Matrikkeldata matrikkel, CancellationToken avbrudd = default);
}
