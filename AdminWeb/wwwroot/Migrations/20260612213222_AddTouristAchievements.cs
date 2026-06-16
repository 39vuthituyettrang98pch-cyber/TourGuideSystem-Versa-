using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddTouristAchievements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tourist_poi_discoveries",
                columns: table => new
                {
                    TouristId = table.Column<int>(type: "INTEGER", nullable: false),
                    PoiId = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscoveryMethod = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PointsAwarded = table.Column<int>(type: "INTEGER", nullable: false),
                    VisitorLatitude = table.Column<decimal>(type: "decimal(10,8)", nullable: true),
                    VisitorLongitude = table.Column<decimal>(type: "decimal(11,8)", nullable: true),
                    DiscoveredAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tourist_poi_discoveries", x => new { x.TouristId, x.PoiId });
                    table.ForeignKey(
                        name: "FK_tourist_poi_discoveries_pois_PoiId",
                        column: x => x.PoiId,
                        principalTable: "pois",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tourist_poi_discoveries_tourists_TouristId",
                        column: x => x.TouristId,
                        principalTable: "tourists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tourist_poi_discoveries_PoiId",
                table: "tourist_poi_discoveries",
                column: "PoiId");

            migrationBuilder.CreateIndex(
                name: "IX_tourist_poi_discoveries_TouristId_DiscoveredAt",
                table: "tourist_poi_discoveries",
                columns: new[] { "TouristId", "DiscoveredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tourist_poi_discoveries");
        }
    }
}
