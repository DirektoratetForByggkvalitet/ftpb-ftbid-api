using Ftpb.Ftbid.Application.Kontrakter;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Validerer og endrer adresser og matrikkelopplysninger for en eksisterende ftbid.</summary>
public interface IFtbidEiendomService
{
    /// <summary>Legger til en adresse uten duplikater. Første adresse blir hovedadresse.</summary>
    Task LeggTilAdresseAsync(string ftbid, string adresse, CancellationToken avbrudd = default);
    /// <summary>Fjerner adressen hvis den finnes og oppdaterer hovedadressen.</summary>
    Task FjernAdresseAsync(string ftbid, string adresse, CancellationToken avbrudd = default);
    /// <summary>Legger til en matrikkelenhet uten duplikater.</summary>
    Task LeggTilMatrikkelAsync(string ftbid, Matrikkeldata matrikkel, CancellationToken avbrudd = default);
    /// <summary>Fjerner matrikkelenheten hvis den finnes, men bevarer minst én matrikkel.</summary>
    Task FjernMatrikkelAsync(string ftbid, Matrikkeldata matrikkel, CancellationToken avbrudd = default);
}
