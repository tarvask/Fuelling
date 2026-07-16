using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FuelStation.ReservationService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pumps",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    IsBusy = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pumps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tanks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    FuelType = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Capacity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    CurrentVolume = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tanks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "nozzles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    FuelType = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    TankId = table.Column<string>(type: "text", nullable: false),
                    PumpId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nozzles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_nozzles_pumps_PumpId",
                        column: x => x.PumpId,
                        principalTable: "pumps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_nozzles_tanks_TankId",
                        column: x => x.TankId,
                        principalTable: "tanks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_nozzles_PumpId",
                table: "nozzles",
                column: "PumpId");

            migrationBuilder.CreateIndex(
                name: "IX_nozzles_TankId",
                table: "nozzles",
                column: "TankId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nozzles");

            migrationBuilder.DropTable(
                name: "pumps");

            migrationBuilder.DropTable(
                name: "tanks");
        }
    }
}
