using System.Text.Json.Serialization;
using Ftpb.Ftbid.Domain.Prosjekter;

namespace Ftpb.Ftbid.Application.Kontrakter;

/// <summary>Eiendom ved oppretting eller erstatning. Identifikasjonen gjelder alle adressene i listen.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class Eiendomsdata
{
    public Eiendomsidentifikasjon? Eiendomsidentifikasjon { get; set; }
    public List<Adressedata> Adresse { get; set; } = [];
    public string? Bygningsnummer { get; set; }
    public string? Bolignummer { get; set; }
    public string? Kommunenavn { get; set; }
}
