using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STailor.Infrastructure.Persistence.Migrations.Local
{
    /// <inheritdoc />
    public partial class AddSyncPullCursors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sync_pull_cursors",
                columns: table => new
                {
                    Scope = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LastSyncedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_pull_cursors", x => x.Scope);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sync_pull_cursors");
        }
    }
}
