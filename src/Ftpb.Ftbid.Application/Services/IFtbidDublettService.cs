using FtbidEntitet = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Vurderer prosjektdata mot eksisterende prosjekter før oppretting.</summary>
public interface IFtbidDublettService
{
    /// <summary>Returnerer det sterkeste treffet, eller null hvis ingen dublettregel treffer.</summary>
    FtbidDublettTreff? Finn(FtbidEntitet prosjekt, IEnumerable<FtbidEntitet> kandidater);
}

/// <summary>Forklarbart dublettreff. Sannsynlig beskriver en regel, ikke en statistisk sannsynlighet.</summary>
public sealed record FtbidDublettTreff(string Ftbid, bool Sannsynlig, IReadOnlyList<string> SamsvarendeFelter);
