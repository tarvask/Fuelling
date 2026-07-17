using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FuelStation.ReservationService.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "delivery_sessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "fuelling_sessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    FuelType = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ReservedVolume = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    ActualVolume = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PumpId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TankId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fuelling_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fuelling_sessions_pumps_PumpId",
                        column: x => x.PumpId,
                        principalTable: "pumps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fuelling_sessions_tanks_TankId",
                        column: x => x.TankId,
                        principalTable: "tanks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_compartments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    FuelType = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Litres = table.Column<double>(type: "double precision", nullable: false),
                    DeliverySessionId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_compartments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_delivery_compartments_delivery_sessions_DeliverySessionId",
                        column: x => x.DeliverySessionId,
                        principalTable: "delivery_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_compartments_DeliverySessionId",
                table: "delivery_compartments",
                column: "DeliverySessionId");

            migrationBuilder.CreateIndex(
                name: "IX_fuelling_sessions_PumpId",
                table: "fuelling_sessions",
                column: "PumpId");

            migrationBuilder.CreateIndex(
                name: "IX_fuelling_sessions_TankId",
                table: "fuelling_sessions",
                column: "TankId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "delivery_compartments");

            migrationBuilder.DropTable(
                name: "fuelling_sessions");

            migrationBuilder.DropTable(
                name: "delivery_sessions");
        }
    }
}
