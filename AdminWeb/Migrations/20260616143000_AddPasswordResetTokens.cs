using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent SQL keeps old hosting/local SQLite databases safe even if the table
            // had already been created by the previous runtime safety-net code.
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS password_reset_tokens (
                    Id INTEGER NOT NULL CONSTRAINT PK_password_reset_tokens PRIMARY KEY AUTOINCREMENT,
                    TouristId INTEGER NOT NULL,
                    Email TEXT NOT NULL,
                    TokenHash TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    ExpiresAt TEXT NOT NULL,
                    UsedAt TEXT NULL,
                    CONSTRAINT FK_password_reset_tokens_tourists_TouristId
                        FOREIGN KEY (TouristId) REFERENCES tourists (Id) ON DELETE CASCADE
                );");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS IX_password_reset_tokens_TokenHash
                    ON password_reset_tokens (TokenHash);");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS IX_password_reset_tokens_TouristId_ExpiresAt
                    ON password_reset_tokens (TouristId, ExpiresAt);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "password_reset_tokens");
        }
    }
}
