using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Greenhouse.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sensor_types",
                columns: table => new
                {
                    kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    unit = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    valid_min = table.Column<double>(type: "REAL", nullable: false),
                    valid_max = table.Column<double>(type: "REAL", nullable: false),
                    comfort_min = table.Column<double>(type: "REAL", nullable: false),
                    comfort_max = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_types", x => x.kind);
                    table.CheckConstraint("ck_sensor_types_comfort_range", "comfort_min <= comfort_max");
                    table.CheckConstraint("ck_sensor_types_valid_range", "valid_min < valid_max");
                });

            migrationBuilder.CreateTable(
                name: "sensors",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensors", x => x.id);
                    table.ForeignKey(
                        name: "FK_sensors_sensor_types_kind",
                        column: x => x.kind,
                        principalTable: "sensor_types",
                        principalColumn: "kind",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alerts",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    sensor_id = table.Column<int>(type: "INTEGER", nullable: false),
                    kind = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    trigger_value = table.Column<double>(type: "REAL", nullable: false),
                    opened_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    closed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    acknowledged_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    acknowledged_by = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerts", x => x.id);
                    table.CheckConstraint("ck_alerts_closed_after_opened", "closed_at IS NULL OR closed_at >= opened_at");
                    table.CheckConstraint("ck_alerts_kind", "kind IN ('low', 'high')");
                    table.ForeignKey(
                        name: "FK_alerts_sensors_sensor_id",
                        column: x => x.sensor_id,
                        principalTable: "sensors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "readings",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    sensor_id = table.Column<int>(type: "INTEGER", nullable: false),
                    value = table.Column<double>(type: "REAL", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_readings", x => x.id);
                    table.ForeignKey(
                        name: "FK_readings_sensors_sensor_id",
                        column: x => x.sensor_id,
                        principalTable: "sensors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "sensor_types",
                columns: new[] { "kind", "comfort_max", "comfort_min", "unit", "valid_max", "valid_min" },
                values: new object[,]
                {
                    { "humidity", 80.0, 50.0, "%", 100.0, 0.0 },
                    { "light", 60000.0, 1000.0, "lx", 150000.0, 0.0 },
                    { "temperature", 28.0, 18.0, "°C", 80.0, -40.0 }
                });

            migrationBuilder.InsertData(
                table: "sensors",
                columns: new[] { "id", "kind", "name" },
                values: new object[,]
                {
                    { 1, "temperature", "temp-1" },
                    { 2, "humidity", "hum-1" },
                    { 3, "light", "light-1" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_alerts_opened_at",
                table: "alerts",
                column: "opened_at");

            migrationBuilder.CreateIndex(
                name: "IX_alerts_sensor_id_closed_at",
                table: "alerts",
                columns: new[] { "sensor_id", "closed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_readings_sensor_id_recorded_at",
                table: "readings",
                columns: new[] { "sensor_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sensors_kind",
                table: "sensors",
                column: "kind");

            migrationBuilder.CreateIndex(
                name: "IX_sensors_name",
                table: "sensors",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerts");

            migrationBuilder.DropTable(
                name: "readings");

            migrationBuilder.DropTable(
                name: "sensors");

            migrationBuilder.DropTable(
                name: "sensor_types");
        }
    }
}
