using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoadIx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantToReportExportQueueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportExportJobs_Status_LastPublishedAtUtc_CreatedAtUtc",
                schema: "reporting",
                table: "ReportExportJobs");

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportJobs_TenantId_Status_LastPublishedAtUtc_CreatedAtUtc",
                schema: "reporting",
                table: "ReportExportJobs",
                columns: new[] { "TenantId", "Status", "LastPublishedAtUtc", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportExportJobs_TenantId_Status_LastPublishedAtUtc_CreatedAtUtc",
                schema: "reporting",
                table: "ReportExportJobs");

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportJobs_Status_LastPublishedAtUtc_CreatedAtUtc",
                schema: "reporting",
                table: "ReportExportJobs",
                columns: new[] { "Status", "LastPublishedAtUtc", "CreatedAtUtc" });
        }
    }
}
