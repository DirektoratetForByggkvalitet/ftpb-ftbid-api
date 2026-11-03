using Ftpb.Ftbid.Domain.Prosjekter;

namespace Ftpb.Ftbid.Application.Matrikkel;

/// <summary>Henter samtlige adresser for én eksakt matrikkelidentifikasjon.</summary>
public interface IMatrikkelAdresseoppslag
{
    /// <summary>Returnerer alle adresser; tekniske feil skal kastes slik at oppslaget kan forsøkes igjen.</summary>
    Task<IReadOnlyList<Eiendomsadresse>> HentAsync(Eiendomsidentifikasjon identifikasjon, CancellationToken avbrudd);
}
