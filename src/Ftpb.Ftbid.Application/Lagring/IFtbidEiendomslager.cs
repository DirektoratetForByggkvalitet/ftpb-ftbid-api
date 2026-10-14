using Ftpb.Ftbid.Application.Kontrakter;

namespace Ftpb.Ftbid.Application.Lagring;

/// <summary>Atomiske endringer av prosjektets adresser og matrikkelenheter.</summary>
public interface IFtbidEiendomslager
{
    /// <summary>Legger til en adresse uten duplikater. Første adresse blir hovedadresse.</summary>
    Task LeggTilAdresseAsync(string ftbid, string adresse, CancellationToken avbrudd);
    /// <summary>Fjerner adressen hvis den finnes og oppdaterer hovedadressen.</summary>
    Task FjernAdresseAsync(string ftbid, string adresse, CancellationToken avbrudd);
    /// <summary>Legger til en matrikkelenhet uten duplikater.</summary>
    Task LeggTilMatrikkelAsync(string ftbid, Matrikkeldata matrikkel, CancellationToken avbrudd);
    /// <summary>Fjerner matrikkelenheten hvis den finnes, men bevarer minst én matrikkel.</summary>
    Task FjernMatrikkelAsync(string ftbid, Matrikkeldata matrikkel, CancellationToken avbrudd);
}
