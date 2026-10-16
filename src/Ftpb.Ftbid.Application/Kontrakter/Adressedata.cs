using System.Text.Json.Serialization;
namespace Ftpb.Ftbid.Application.Kontrakter;

/// <summary>Strukturert adresse i forespørsler og svar. Postnummer og husnummer bevarer formateringen.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Adressedata(string? Adresselinje1 = null, string? Adresselinje2 = null,
    string? Adresselinje3 = null, string? Postnr = null, string? Poststed = null, string? Landkode = null,
    string? Gatenavn = null, string? Husnr = null, string? Bokstav = null);
