using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ftpb.Ftbid.Infrastructure.Migrasjoner
{
    /// <inheritdoc />
    public partial class FjernFtIdType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ft_id_type",
                table: "ftbid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ft_id_type",
                table: "ftbid",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
