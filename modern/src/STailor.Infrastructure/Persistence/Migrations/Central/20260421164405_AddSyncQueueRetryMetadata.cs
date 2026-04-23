using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STailor.Infrastructure.Persistence.Migrations.Central
{
    /// <inheritdoc />
    public partial class AddSyncQueueRetryMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "sync_queue_items",
                type: "character varying(220)",
                maxLength: 220,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAtUtc",
                table: "sync_queue_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_sync_queue_items_IdempotencyKey",
                table: "sync_queue_items",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sync_queue_items_NextAttemptAtUtc",
                table: "sync_queue_items",
                column: "NextAttemptAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sync_queue_items_IdempotencyKey",
                table: "sync_queue_items");

            migrationBuilder.DropIndex(
                name: "IX_sync_queue_items_NextAttemptAtUtc",
                table: "sync_queue_items");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "sync_queue_items");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtUtc",
                table: "sync_queue_items");
        }
    }
}
