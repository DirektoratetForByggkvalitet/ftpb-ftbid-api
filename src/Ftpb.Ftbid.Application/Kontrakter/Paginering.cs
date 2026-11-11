using System.ComponentModel.DataAnnotations;

namespace Ftpb.Ftbid.Application.Kontrakter;

/// <summary>Side velges fra 1, med fast sidestørrelse på 50.</summary>
public sealed class Paginering
{
    public const int Sidestorrelse = 50;

    [Range(1, int.MaxValue / Sidestorrelse + 1)]
    public int Side { get; set; } = 1;

    public int BeregnForskyvning() => checked((Side - 1) * Sidestorrelse);
}

/// <summary>En side med resultater og informasjon om neste side.</summary>
public sealed record PaginertResultat<T>(IReadOnlyList<T> Resultater, int Side, bool HarNesteSide)
{
    public int Sidestorrelse => Paginering.Sidestorrelse;

    /// <summary>Bruker én ekstra rad til å avgjøre om neste side finnes.</summary>
    public static PaginertResultat<T> Opprett(IReadOnlyList<T> rader, int side) =>
        new(rader.Take(Paginering.Sidestorrelse).ToList(), side, rader.Count > Paginering.Sidestorrelse);
}
