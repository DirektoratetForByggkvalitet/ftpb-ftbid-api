using System.ComponentModel.DataAnnotations;

namespace Ftpb.Ftbid.Application.Contracts;

/// <summary>Side velges fra 1, med fast sidestørrelse på 50.</summary>
public sealed class Pagination
{
    public const int Sidestorrelse = 50;

    [Range(1, int.MaxValue / Sidestorrelse + 1)]
    public int Side { get; set; } = 1;

    public int CalculateOffset() => checked((Side - 1) * Sidestorrelse);
}

/// <summary>En side med resultater og informasjon om neste side.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Resultater, int Side, bool HarNesteSide)
{
    public int Sidestorrelse => Pagination.Sidestorrelse;

    /// <summary>Bruker én ekstra rad til å avgjøre om neste side finnes.</summary>
    public static PagedResult<T> Create(IReadOnlyList<T> rows, int side) =>
        new(rows.Take(Pagination.Sidestorrelse).ToList(), side, rows.Count > Pagination.Sidestorrelse);
}
