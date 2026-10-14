using Ftpb.Ftbid.Application.Kontrakter;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Autoritativ kontroll av matrikkelopplysninger før tilknytning til ftbid.</summary>
public interface IMatrikkelService
{
    /// <summary>Krever et eksakt treff i Matrikkelen; tomt svar eller oppslagsfeil avvises.</summary>
    Task ValiderAsync(Matrikkeldata matrikkel, CancellationToken avbrudd = default);
}
