using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewAndBookmark : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "poi_reviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PoiId = table.Column<int>(type: "INTEGER", nullable: false),
                    TouristId = table.Column<int>(type: "INTEGER", nullable: false),
                    Rating = table.Column<int>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poi_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_poi_reviews_pois_PoiId",
                        column: x => x.PoiId,
                        principalTable: "pois",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_poi_reviews_tourists_TouristId",
                        column: x => x.TouristId,
                        principalTable: "tourists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tourist_bookmarks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TouristId = table.Column<int>(type: "INTEGER", nullable: false),
                    PoiId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tourist_bookmarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tourist_bookmarks_pois_PoiId",
                        column: x => x.PoiId,
                        principalTable: "pois",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tourist_bookmarks_tourists_TouristId",
                        column: x => x.TouristId,
                        principalTable: "tourists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_poi_reviews_PoiId",
                table: "poi_reviews",
                column: "PoiId");

            migrationBuilder.CreateIndex(
                name: "IX_poi_reviews_TouristId",
                table: "poi_reviews",
                column: "TouristId");

            migrationBuilder.CreateIndex(
                name: "IX_tourist_bookmarks_PoiId",
                table: "tourist_bookmarks",
                column: "PoiId");

            migrationBuilder.CreateIndex(
                name: "IX_tourist_bookmarks_TouristId",
                table: "tourist_bookmarks",
                column: "TouristId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "poi_reviews");

            migrationBuilder.DropTable(
                name: "tourist_bookmarks");
        }
    }
}
