using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AdminWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddMultilingualMediaTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_poi_translations_PoiId",
                table: "poi_translations");

            migrationBuilder.AddColumn<string>(
                name: "VideoUrl",
                table: "poi_translations",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "media_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PoiId = table.Column<int>(type: "INTEGER", nullable: false),
                    TaskType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ProgressPercentage = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_tasks", x => x.Id);
                    table.CheckConstraint("CK_media_tasks_progress_percentage", "\"ProgressPercentage\" >= 0 AND \"ProgressPercentage\" <= 100");
                    table.ForeignKey(
                        name: "FK_media_tasks_pois_PoiId",
                        column: x => x.PoiId,
                        principalTable: "pois",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supported_languages",
                columns: table => new
                {
                    LanguageCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    LanguageName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EdgeTtsVoice = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supported_languages", x => x.LanguageCode);
                });

            migrationBuilder.InsertData(
                table: "supported_languages",
                columns: new[] { "LanguageCode", "EdgeTtsVoice", "IsActive", "LanguageName" },
                values: new object[,]
                {
                    { "en", "en-US-AriaNeural", true, "English" },
                    { "ja", "ja-JP-NanamiNeural", true, "Japanese" },
                    { "ko", "ko-KR-SunHiNeural", true, "Korean" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_poi_translations_PoiId_LanguageCode",
                table: "poi_translations",
                columns: new[] { "PoiId", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_tasks_PoiId",
                table: "media_tasks",
                column: "PoiId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "media_tasks");

            migrationBuilder.DropTable(
                name: "supported_languages");

            migrationBuilder.DropIndex(
                name: "IX_poi_translations_PoiId_LanguageCode",
                table: "poi_translations");

            migrationBuilder.DropColumn(
                name: "VideoUrl",
                table: "poi_translations");

            migrationBuilder.CreateIndex(
                name: "IX_poi_translations_PoiId",
                table: "poi_translations",
                column: "PoiId");
        }
    }
}
