using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ftpb.Ftbid.Infrastructure.Migrasjoner
{
    /// <summary>Oppretter ftbid, identifikasjonsmetadata og tilhørende referansetabeller.</summary>
    public partial class OpprettFtbidOgIdentifikasjonsmetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrasjon)
        {
            migrasjon.CreateSequence(
                name: "ftbid_sekvens",
                maxValue: 9999999L);

            migrasjon.CreateTable(
                name: "ftbid",
                columns: table => new
                {
                    ean = table.Column<string>(type: "character varying(18)", maxLength: 18, nullable: false),
                    kommunenummer = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    gaardsnummer = table.Column<int>(type: "integer", nullable: false),
                    bruksnummer = table.Column<int>(type: "integer", nullable: false),
                    festenummer = table.Column<int>(type: "integer", nullable: false),
                    seksjonsnummer = table.Column<int>(type: "integer", nullable: false),
                    adresse = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    prosjektnavn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    normalisert_prosjektnavn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    prosjektnummer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    system = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    utstedt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ftbid", x => x.ean);
                });

            migrasjon.CreateTable(
                name: "kommune",
                columns: table => new
                {
                    kommunenummer = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    gyldig_fra = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    gyldig_til = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    nytt_kommunenummer = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kommune", x => x.kommunenummer);
                });

            migrasjon.CreateTable(
                name: "skjemametadata",
                columns: table => new
                {
                    arkivreferanse = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    skjematype = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tiltakstyper = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skjemametadata", x => x.arkivreferanse);
                });

            migrasjon.CreateTable(
                name: "adresse",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ftbid = table.Column<string>(type: "character varying(18)", nullable: false),
                    rekkefolge = table.Column<int>(type: "integer", nullable: false),
                    adressetekst = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adresse", x => x.id);
                    table.ForeignKey(
                        name: "FK_adresse_ftbid_ftbid",
                        column: x => x.ftbid,
                        principalTable: "ftbid",
                        principalColumn: "ean",
                        onDelete: ReferentialAction.Cascade);
                });

            migrasjon.CreateTable(
                name: "matrikkelopplysning",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ftbid = table.Column<string>(type: "character varying(18)", nullable: false),
                    rekkefolge = table.Column<int>(type: "integer", nullable: false),
                    kommunenummer = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    gaardsnummer = table.Column<int>(type: "integer", nullable: false),
                    bruksnummer = table.Column<int>(type: "integer", nullable: false),
                    festenummer = table.Column<int>(type: "integer", nullable: false),
                    seksjonsnummer = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matrikkelopplysning", x => x.id);
                    table.ForeignKey(
                        name: "FK_matrikkelopplysning_ftbid_ftbid",
                        column: x => x.ftbid,
                        principalTable: "ftbid",
                        principalColumn: "ean",
                        onDelete: ReferentialAction.Cascade);
                });

            migrasjon.CreateTable(
                name: "identifikasjonsmetadata",
                columns: table => new
                {
                    arkivreferanse = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ftbid = table.Column<string>(type: "character varying(18)", maxLength: 18, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identifikasjonsmetadata", x => x.arkivreferanse);
                    table.ForeignKey(
                        name: "FK_identifikasjonsmetadata_ftbid_ftbid",
                        column: x => x.ftbid,
                        principalTable: "ftbid",
                        principalColumn: "ean",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_identifikasjonsmetadata_skjemametadata_arkivreferanse",
                        column: x => x.arkivreferanse,
                        principalTable: "skjemametadata",
                        principalColumn: "arkivreferanse",
                        onDelete: ReferentialAction.Restrict);
                });

            migrasjon.CreateIndex(
                name: "IX_adresse_ftbid_rekkefolge",
                table: "adresse",
                columns: new[] { "ftbid", "rekkefolge" },
                unique: true);

            migrasjon.CreateIndex(
                name: "IX_ftbid_prosjektidentitet",
                table: "ftbid",
                columns: new[] { "kommunenummer", "gaardsnummer", "bruksnummer", "normalisert_prosjektnavn" },
                unique: true);

            migrasjon.CreateIndex(
                name: "IX_identifikasjonsmetadata_ftbid",
                table: "identifikasjonsmetadata",
                column: "ftbid");

            migrasjon.CreateIndex(
                name: "IX_matrikkelopplysning_ftbid_rekkefolge",
                table: "matrikkelopplysning",
                columns: new[] { "ftbid", "rekkefolge" },
                unique: true);

            migrasjon.CreateIndex(
                name: "IX_matrikkelopplysning_kommunenummer_gaardsnummer_bruksnummer",
                table: "matrikkelopplysning",
                columns: new[] { "kommunenummer", "gaardsnummer", "bruksnummer" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrasjon)
        {
            migrasjon.DropTable(
                name: "adresse");

            migrasjon.DropTable(
                name: "identifikasjonsmetadata");

            migrasjon.DropTable(
                name: "kommune");

            migrasjon.DropTable(
                name: "matrikkelopplysning");

            migrasjon.DropTable(
                name: "skjemametadata");

            migrasjon.DropTable(
                name: "ftbid");

            migrasjon.DropSequence(
                name: "ftbid_sekvens");
        }
    }
}
