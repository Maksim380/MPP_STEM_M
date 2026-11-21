using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Greenhouse.Core.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "greenhouses",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    location = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_greenhouses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sensor_types",
                columns: table => new
                {
                    kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    unit = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    valid_min = table.Column<double>(type: "REAL", nullable: false),
                    valid_max = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_types", x => x.kind);
                    table.CheckConstraint("ck_sensor_types_valid_range", "valid_min < valid_max");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    username = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    password_hash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    role = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("ck_users_role", "role IN ('admin', 'operator', 'viewer')");
                });

            migrationBuilder.CreateTable(
                name: "sensors",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    greenhouse_id = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensors", x => x.id);
                    table.ForeignKey(
                        name: "FK_sensors_greenhouses_greenhouse_id",
                        column: x => x.greenhouse_id,
                        principalTable: "greenhouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sensors_sensor_types_kind",
                        column: x => x.kind,
                        principalTable: "sensor_types",
                        principalColumn: "kind",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "thresholds",
                columns: table => new
                {
                    greenhouse_id = table.Column<int>(type: "INTEGER", nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    comfort_min = table.Column<double>(type: "REAL", nullable: false),
                    comfort_max = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_thresholds", x => new { x.greenhouse_id, x.kind });
                    table.CheckConstraint("ck_thresholds_range", "comfort_min <= comfort_max");
                    table.ForeignKey(
                        name: "FK_thresholds_greenhouses_greenhouse_id",
                        column: x => x.greenhouse_id,
                        principalTable: "greenhouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_thresholds_sensor_types_kind",
                        column: x => x.kind,
                        principalTable: "sensor_types",
                        principalColumn: "kind",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    user_id = table.Column<int>(type: "INTEGER", nullable: false),
                    token_hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                table: "greenhouses",
                columns: new[] { "id", "created_at", "location", "name" },
                values: new object[] { 1, new DateTime(2026, 11, 21, 0, 0, 0, 0, DateTimeKind.Utc), "Навчальна ділянка", "Теплиця №1" });

            migrationBuilder.InsertData(
                table: "sensor_types",
                columns: new[] { "kind", "unit", "valid_max", "valid_min" },
                values: new object[,]
                {
                    { "humidity", "%", 100.0, 0.0 },
                    { "light", "lx", 150000.0, 0.0 },
                    { "temperature", "°C", 80.0, -40.0 }
                });

            migrationBuilder.InsertData(
                table: "sensors",
                columns: new[] { "id", "greenhouse_id", "kind", "name" },
                values: new object[,]
                {
                    { 1, 1, "temperature", "temp-1" },
                    { 2, 1, "humidity", "hum-1" },
                    { 3, 1, "light", "light-1" }
                });

            migrationBuilder.InsertData(
                table: "thresholds",
                columns: new[] { "greenhouse_id", "kind", "comfort_max", "comfort_min" },
                values: new object[,]
                {
                    { 1, "humidity", 80.0, 50.0 },
                    { 1, "light", 60000.0, 1000.0 },
                    { 1, "temperature", 28.0, 18.0 }
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
                name: "IX_greenhouses_name",
                table: "greenhouses",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_readings_sensor_id_recorded_at",
                table: "readings",
                columns: new[] { "sensor_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_sensors_greenhouse_id",
                table: "sensors",
                column: "greenhouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_sensors_kind",
                table: "sensors",
                column: "kind");

            migrationBuilder.CreateIndex(
                name: "IX_sensors_name",
                table: "sensors",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_thresholds_kind",
                table: "thresholds",
                column: "kind");

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
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
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "thresholds");

            migrationBuilder.DropTable(
                name: "sensors");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "greenhouses");

            migrationBuilder.DropTable(
                name: "sensor_types");
        }
    }
}
