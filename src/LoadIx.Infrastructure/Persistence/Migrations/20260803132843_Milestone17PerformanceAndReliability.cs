using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoadIx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Milestone17PerformanceAndReliability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeadLetteredAtUtc",
                schema: "operations",
                table: "OperationalOutboxMessages",
                type: "datetimeoffset(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAtUtc",
                schema: "operations",
                table: "OperationalOutboxMessages",
                type: "datetimeoffset(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TenantId_Status_PlannedDeliveryAtUtc_Id",
                schema: "trips",
                table: "Trips",
                columns: new[] { "TenantId", "Status", "PlannedDeliveryAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TripLocationUpdates_TenantId_CapturedAtUtc_TripId_Id",
                schema: "trips",
                table: "TripLocationUpdates",
                columns: new[] { "TenantId", "CapturedAtUtc", "TripId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalOutboxMessages_DeadLetteredAtUtc_DispatchedAtUtc_NextAttemptAtUtc_OccurredAtUtc_Id",
                schema: "operations",
                table: "OperationalOutboxMessages",
                columns: new[] { "DeadLetteredAtUtc", "DispatchedAtUtc", "NextAttemptAtUtc", "OccurredAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trips_TenantId_Status_PlannedDeliveryAtUtc_Id",
                schema: "trips",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_TripLocationUpdates_TenantId_CapturedAtUtc_TripId_Id",
                schema: "trips",
                table: "TripLocationUpdates");

            migrationBuilder.DropIndex(
                name: "IX_OperationalOutboxMessages_DeadLetteredAtUtc_DispatchedAtUtc_NextAttemptAtUtc_OccurredAtUtc_Id",
                schema: "operations",
                table: "OperationalOutboxMessages");

            migrationBuilder.DropColumn(
                name: "DeadLetteredAtUtc",
                schema: "operations",
                table: "OperationalOutboxMessages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtUtc",
                schema: "operations",
                table: "OperationalOutboxMessages");
        }
    }
}
