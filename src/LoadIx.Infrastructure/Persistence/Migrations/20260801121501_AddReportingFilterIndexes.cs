using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoadIx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingFilterIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trips_TenantId_CustomerId",
                schema: "trips",
                table: "Trips");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TenantId_CustomerId_CompletedAtUtc_Id",
                schema: "trips",
                table: "Trips",
                columns: new[] { "TenantId", "CustomerId", "CompletedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TenantId_CustomerId_PlannedDeliveryAtUtc_Id",
                schema: "trips",
                table: "Trips",
                columns: new[] { "TenantId", "CustomerId", "PlannedDeliveryAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceBatches_TenantId_CustomerId_CreatedAtUtc_Id",
                schema: "finance",
                table: "InvoiceBatches",
                columns: new[] { "TenantId", "CustomerId", "CreatedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trips_TenantId_CustomerId_CompletedAtUtc_Id",
                schema: "trips",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Trips_TenantId_CustomerId_PlannedDeliveryAtUtc_Id",
                schema: "trips",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceBatches_TenantId_CustomerId_CreatedAtUtc_Id",
                schema: "finance",
                table: "InvoiceBatches");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TenantId_CustomerId",
                schema: "trips",
                table: "Trips",
                columns: new[] { "TenantId", "CustomerId" });
        }
    }
}
