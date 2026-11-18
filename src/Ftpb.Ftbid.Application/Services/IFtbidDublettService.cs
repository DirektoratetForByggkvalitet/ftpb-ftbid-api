using FtbidEntity = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Vurderer prosjektdata mot eksisterende prosjekter før oppretting.</summary>
public interface IFtbidDublettService
{
    /// <summary>Returnerer første dublett sortert på ftbid, eller null hvis kombinasjonen ikke finnes.</summary>
    FtbidDublettTreff? Finn(FtbidEntity prosjekt, IEnumerable<FtbidEntity> kandidater);
}

/// <summary>Dublettreff med eksisterende ftbid og feltene som identifiserer dubletten.</summary>
public sealed record FtbidDublettTreff(string Ftbid, IReadOnlyList<string> SamsvarendeFelter);
