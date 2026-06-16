using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AdminWeb.Migrations
{
    /// <inheritdoc />
    public partial class AlignWebLanguagesWithMobile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "supported_languages",
                columns: new[] { "LanguageCode", "EdgeTtsVoice", "IsActive", "LanguageName" },
                values: new object[,]
                {
                    { "vi", "vi-VN-HoaiMyNeural", true, "Tiếng Việt" },
                    { "zh", "zh-CN-XiaoxiaoNeural", true, "中文" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "supported_languages",
                keyColumn: "LanguageCode",
                keyValue: "vi");

            migrationBuilder.DeleteData(
                table: "supported_languages",
                keyColumn: "LanguageCode",
                keyValue: "zh");
        }
    }
}
