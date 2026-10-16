using Ftpb.Ftbid.Domain.Prosjekter;

namespace Ftpb.Ftbid.Application.Lagring;

/// <summary>Atomiske endringer av eiendommer på en ftbid.</summary>
public interface IFtbidEiendomslager
{
    /// <summary>Legger til en eiendom.</summary>
    Task LeggTilAsync(string ftbid, Eiendom eiendom, CancellationToken avbrudd);
    /// <summary>Erstatter en eiendom og dens adresser, med samme ID.</summary>
    Task ErstattAsync(string ftbid, long eiendomId, Eiendom eiendom, CancellationToken avbrudd);
    /// <summary>Fjerner eiendommen hvis den finnes. Også siste eiendom kan fjernes.</summary>
    Task FjernAsync(string ftbid, long eiendomId, CancellationToken avbrudd);
}
