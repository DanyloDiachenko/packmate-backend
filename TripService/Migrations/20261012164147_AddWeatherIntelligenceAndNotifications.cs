using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripService.Migrations
{
    public partial class AddWeatherIntelligenceAndNotifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasWeatherImpact",
                table: "trips",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastWeatherCheckAt",
                table: "trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "trip_weather_forecasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    ForecastDate = table.Column<DateOnly>(type: "date", nullable: false),
                    HighTemp = table.Column<int>(type: "integer", nullable: false),
                    LowTemp = table.Column<int>(type: "integer", nullable: false),
                    Condition = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IconName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RainProbability = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    UvIndex = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    WindSpeed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_weather_forecasts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trip_weather_forecasts_trips_TripId",
                        column: x => x.TripId,
                        principalTable: "trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trip_weather_impacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Highlights = table.Column<string>(type: "jsonb", nullable: false),
                    Proposals = table.Column<string>(type: "jsonb", nullable: false),
                    ExistingMatchedNotice = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_weather_impacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trip_weather_impacts_trips_TripId",
                        column: x => x.TripId,
                        principalTable: "trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    ActionTitle = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RelatedDestination = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_messages_trips_TripId",
                        column: x => x.TripId,
                        principalTable: "trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trip_weather_forecasts_TripId_ForecastDate",
                table: "trip_weather_forecasts",
                columns: new[] { "TripId", "ForecastDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trip_weather_impacts_TripId",
                table: "trip_weather_impacts",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_user_messages_TripId",
                table: "user_messages",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_user_messages_UserId_IsRead",
                table: "user_messages",
                columns: new[] { "UserId", "IsRead" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trip_weather_forecasts");

            migrationBuilder.DropTable(
                name: "trip_weather_impacts");

            migrationBuilder.DropTable(
                name: "user_messages");

            migrationBuilder.DropColumn(
                name: "HasWeatherImpact",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "LastWeatherCheckAt",
                table: "trips");
        }
    }
}
