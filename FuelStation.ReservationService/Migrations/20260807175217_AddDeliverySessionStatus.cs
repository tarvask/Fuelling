using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FuelStation.ReservationService.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliverySessionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "delivery_sessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "delivery_sessions");
        }
    }
}
