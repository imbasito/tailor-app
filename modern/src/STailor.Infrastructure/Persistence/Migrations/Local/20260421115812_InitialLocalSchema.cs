using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STailor.Infrastructure.Persistence.Migrations.Local
{
    /// <inheritdoc />
    public partial class InitialLocalSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    City = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    BaselineMeasurementsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sync_queue_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Operation = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    EntityUpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EnqueuedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastAttemptedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SyncedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_queue_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GarmentType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    MeasurementSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    PhotoAttachmentsJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AmountCharged = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AmountPaid = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    TrialScheduledAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    TrialScheduleStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_orders_customer_profiles_CustomerProfileId",
                        column: x => x.CustomerProfileId,
                        principalTable: "customer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PaidAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payments_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orders_CustomerProfileId",
                table: "orders",
                column: "CustomerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_OrderId",
                table: "payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_sync_queue_items_EnqueuedAtUtc",
                table: "sync_queue_items",
                column: "EnqueuedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_sync_queue_items_Status",
                table: "sync_queue_items",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "sync_queue_items");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "customer_profiles");
        }
    }
}
