using Ftpb.Ftbid.Domain.Referansedata;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Ftpb.Ftbid.Infrastructure.Referansedata;

/// <summary>Referansedata som eksporteres fra kommuneregisteret og adapterens skjemametadata.</summary>
public sealed class Referansedata
{
    public List<Kommune> Kommuner { get; set; } = [];
    public List<Skjemametadata> Skjemaer { get; set; } = [];
}
