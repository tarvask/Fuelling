using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FuelStation.ReservationService.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "fuelling_sessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: DateTime.UtcNow);
            
            migrationBuilder.AddColumn<DateTime>(
                name: "FinishedAt",
                table: "fuelling_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "delivery_sessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: DateTime.UtcNow);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinishedAt",
                table: "delivery_sessions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "fuelling_sessions");
            
            migrationBuilder.DropColumn(
                name: "FinishedAt",
                table: "fuelling_sessions");
            
            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "delivery_sessions");
            
            migrationBuilder.DropColumn(
                name: "FinishedAt",
                table: "delivery_sessions");
        }
    }
}
