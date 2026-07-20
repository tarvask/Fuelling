using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FuelStation.ReservationService.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIsBusyFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBusy",
                table: "pumps");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBusy",
                table: "pumps",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
