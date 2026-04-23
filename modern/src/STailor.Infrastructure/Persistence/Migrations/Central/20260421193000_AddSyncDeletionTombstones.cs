using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STailor.Infrastructure.Persistence.Migrations.Central
{
    /// <inheritdoc />
    public partial class AddSyncDeletionTombstones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sync_deletion_tombstones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_deletion_tombstones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sync_deletion_tombstones_DeletedAtUtc",
                table: "sync_deletion_tombstones",
                column: "DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_sync_deletion_tombstones_EntityType_EntityId",
                table: "sync_deletion_tombstones",
                columns: new[] { "EntityType", "EntityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sync_deletion_tombstones");
        }
    }
}
