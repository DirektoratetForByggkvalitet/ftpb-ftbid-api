using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ftpb.Ftbid.Infrastructure.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilDublettopplysninger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ftbid_prosjektidentitet",
                table: "ftbid");

            migrationBuilder.AddColumn<string>(
                name: "bygningsnummer",
                table: "ftbid",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ekstern_prosjektreferanse",
                table: "ftbid",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kommunalt_saksnummer",
                table: "ftbid",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "organisasjonsnummer",
                table: "ftbid",
                type: "character varying(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tiltakshaver",
                table: "ftbid",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tiltakstype",
                table: "ftbid",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ftbid_kommunenummer",
                table: "ftbid",
                column: "kommunenummer");

            migrationBuilder.CreateIndex(
                name: "IX_ftbid_prosjektidentitet",
                table: "ftbid",
                columns: new[] { "kommunenummer", "gaardsnummer", "bruksnummer", "festenummer", "seksjonsnummer", "normalisert_prosjektnavn" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ftbid_kommunenummer",
                table: "ftbid");

            migrationBuilder.DropIndex(
                name: "IX_ftbid_prosjektidentitet",
                table: "ftbid");

            migrationBuilder.DropColumn(
                name: "bygningsnummer",
                table: "ftbid");

            migrationBuilder.DropColumn(
                name: "ekstern_prosjektreferanse",
                table: "ftbid");

            migrationBuilder.DropColumn(
                name: "kommunalt_saksnummer",
                table: "ftbid");

            migrationBuilder.DropColumn(
                name: "organisasjonsnummer",
                table: "ftbid");

            migrationBuilder.DropColumn(
                name: "tiltakshaver",
                table: "ftbid");

            migrationBuilder.DropColumn(
                name: "tiltakstype",
                table: "ftbid");

            migrationBuilder.CreateIndex(
                name: "IX_ftbid_prosjektidentitet",
                table: "ftbid",
                columns: new[] { "kommunenummer", "gaardsnummer", "bruksnummer", "normalisert_prosjektnavn" },
                unique: true);
        }
    }
}
