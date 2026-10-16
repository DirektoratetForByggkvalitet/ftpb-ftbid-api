using Ftpb.Ftbid.Application.Kontrakter;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Endrer eiendommer med strukturerte adresser og identifikasjon.</summary>
public interface IFtbidEiendomService
{
    /// <summary>Legger til en eiendom.</summary>
    Task LeggTilAsync(string ftbid, Eiendomsdata eiendom, CancellationToken avbrudd = default);
    /// <summary>Erstatter eiendommen med angitt ID.</summary>
    Task ErstattAsync(string ftbid, long eiendomId, Eiendomsdata eiendom, CancellationToken avbrudd = default);
    /// <summary>Fjerner eiendommen hvis den finnes, også når den er den siste.</summary>
    Task FjernAsync(string ftbid, long eiendomId, CancellationToken avbrudd = default);
}
