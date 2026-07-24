using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FuelStation.ReservationService.Migrations
{
    /// <inheritdoc />
    public partial class InitialMultiStation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StationId",
                table: "tanks",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StationId",
                table: "pumps",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StationId",
                table: "fuelling_sessions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StationId",
                table: "delivery_sessions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "stations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tanks_StationId",
                table: "tanks",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_pumps_StationId",
                table: "pumps",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_fuelling_sessions_StationId",
                table: "fuelling_sessions",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_sessions_StationId",
                table: "delivery_sessions",
                column: "StationId");

            migrationBuilder.AddForeignKey(
                name: "FK_delivery_sessions_stations_StationId",
                table: "delivery_sessions",
                column: "StationId",
                principalTable: "stations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_fuelling_sessions_stations_StationId",
                table: "fuelling_sessions",
                column: "StationId",
                principalTable: "stations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_pumps_stations_StationId",
                table: "pumps",
                column: "StationId",
                principalTable: "stations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_tanks_stations_StationId",
                table: "tanks",
                column: "StationId",
                principalTable: "stations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_delivery_sessions_stations_StationId",
                table: "delivery_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_fuelling_sessions_stations_StationId",
                table: "fuelling_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_pumps_stations_StationId",
                table: "pumps");

            migrationBuilder.DropForeignKey(
                name: "FK_tanks_stations_StationId",
                table: "tanks");

            migrationBuilder.DropTable(
                name: "stations");

            migrationBuilder.DropIndex(
                name: "IX_tanks_StationId",
                table: "tanks");

            migrationBuilder.DropIndex(
                name: "IX_pumps_StationId",
                table: "pumps");

            migrationBuilder.DropIndex(
                name: "IX_fuelling_sessions_StationId",
                table: "fuelling_sessions");

            migrationBuilder.DropIndex(
                name: "IX_delivery_sessions_StationId",
                table: "delivery_sessions");

            migrationBuilder.DropColumn(
                name: "StationId",
                table: "tanks");

            migrationBuilder.DropColumn(
                name: "StationId",
                table: "pumps");

            migrationBuilder.DropColumn(
                name: "StationId",
                table: "fuelling_sessions");

            migrationBuilder.DropColumn(
                name: "StationId",
                table: "delivery_sessions");
        }
    }
}
