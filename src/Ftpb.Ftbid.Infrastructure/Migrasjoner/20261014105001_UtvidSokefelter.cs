using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ftpb.Ftbid.Infrastructure.Migrasjoner
{
    /// <inheritdoc />
    public partial class UtvidSokefelter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ansvarlig_soker_organisasjonsnummer",
                table: "ftbid",
                type: "character varying(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bruksenhetsnummer",
                table: "ftbid",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ft_id_type",
                table: "ftbid",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ansvarlig_soker_organisasjonsnummer",
                table: "ftbid");

            migrationBuilder.DropColumn(
                name: "bruksenhetsnummer",
                table: "ftbid");

            migrationBuilder.DropColumn(
                name: "ft_id_type",
                table: "ftbid");
        }
    }
}
