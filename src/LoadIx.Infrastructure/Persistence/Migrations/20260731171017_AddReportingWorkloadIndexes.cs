using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoadIx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingWorkloadIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_TenantId_IsActive_LicenceExpiry_RoadworthyExpiry",
                schema: "fleet",
                table: "Vehicles",
                columns: new[] { "TenantId", "IsActive", "LicenceExpiry", "RoadworthyExpiry" });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TenantId_CompletedAtUtc_Status_Id",
                schema: "trips",
                table: "Trips",
                columns: new[] { "TenantId", "CompletedAtUtc", "Status", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TenantId_PlannedDeliveryAtUtc_Id",
                schema: "trips",
                table: "Trips",
                columns: new[] { "TenantId", "PlannedDeliveryAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TripFinancials_TenantId_CalculatedAtUtc_ApprovalStatus_TripId",
                schema: "finance",
                table: "TripFinancials",
                columns: new[] { "TenantId", "CalculatedAtUtc", "ApprovalStatus", "TripId" });

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_TenantId_IsActive_LicenceExpiry",
                schema: "fleet",
                table: "Drivers",
                columns: new[] { "TenantId", "IsActive", "LicenceExpiry" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_TenantId_IsActive_LicenceExpiry_RoadworthyExpiry",
                schema: "fleet",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Trips_TenantId_CompletedAtUtc_Status_Id",
                schema: "trips",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Trips_TenantId_PlannedDeliveryAtUtc_Id",
                schema: "trips",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_TripFinancials_TenantId_CalculatedAtUtc_ApprovalStatus_TripId",
                schema: "finance",
                table: "TripFinancials");

            migrationBuilder.DropIndex(
                name: "IX_Drivers_TenantId_IsActive_LicenceExpiry",
                schema: "fleet",
                table: "Drivers");
        }
    }
}
