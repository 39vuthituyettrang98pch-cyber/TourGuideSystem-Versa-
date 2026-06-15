using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminWeb.Migrations
{
    /// <inheritdoc />
    public partial class StabilizeMediaTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "media_tasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "media_tasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedLanguages",
                table: "media_tasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "media_tasks",
                type: "TEXT",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "media_tasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SucceededLanguages",
                table: "media_tasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalLanguages",
                table: "media_tasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_media_tasks_Status_CreatedAt",
                table: "media_tasks",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_media_tasks_Status_CreatedAt",
                table: "media_tasks");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "media_tasks");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "media_tasks");

            migrationBuilder.DropColumn(
                name: "FailedLanguages",
                table: "media_tasks");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "media_tasks");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "media_tasks");

            migrationBuilder.DropColumn(
                name: "SucceededLanguages",
                table: "media_tasks");

            migrationBuilder.DropColumn(
                name: "TotalLanguages",
                table: "media_tasks");
        }
    }
}
