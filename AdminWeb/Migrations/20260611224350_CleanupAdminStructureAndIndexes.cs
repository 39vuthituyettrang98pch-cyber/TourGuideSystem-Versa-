using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminWeb.Migrations
{
    /// <inheritdoc />
    public partial class CleanupAdminStructureAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_sync_versions");

            migrationBuilder.DropTable(
                name: "OwnerUpgradeRequests");

            migrationBuilder.DropIndex(
                name: "IX_tour_translations_TourId",
                table: "tour_translations");

            migrationBuilder.DropIndex(
                name: "IX_category_translations_CategoryId",
                table: "category_translations");

            migrationBuilder.CreateIndex(
                name: "IX_tour_translations_TourId_LanguageCode",
                table: "tour_translations",
                columns: new[] { "TourId", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_system_settings_SettingKey",
                table: "system_settings",
                column: "SettingKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sync_versions_VersionNumber",
                table: "sync_versions",
                column: "VersionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_category_translations_CategoryId_LanguageCode",
                table: "category_translations",
                columns: new[] { "CategoryId", "LanguageCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tour_translations_TourId_LanguageCode",
                table: "tour_translations");

            migrationBuilder.DropIndex(
                name: "IX_system_settings_SettingKey",
                table: "system_settings");

            migrationBuilder.DropIndex(
                name: "IX_sync_versions_VersionNumber",
                table: "sync_versions");

            migrationBuilder.DropIndex(
                name: "IX_category_translations_CategoryId_LanguageCode",
                table: "category_translations");

            migrationBuilder.CreateTable(
                name: "data_sync_versions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsForceUpdate = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReleaseNotes = table.Column<string>(type: "TEXT", nullable: true),
                    VersionNumber = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_sync_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OwnerUpgradeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AdminNote = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    OwnerPublicName = table.Column<string>(type: "TEXT", nullable: true),
                    RequesterUserId = table.Column<string>(type: "TEXT", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnerUpgradeRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tour_translations_TourId",
                table: "tour_translations",
                column: "TourId");

            migrationBuilder.CreateIndex(
                name: "IX_category_translations_CategoryId",
                table: "category_translations",
                column: "CategoryId");
        }
    }
}
