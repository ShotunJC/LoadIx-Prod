using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoadIx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenReportingExports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ContentLengthBytes",
                schema: "reporting",
                table: "ReportExportJobs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FromDate",
                schema: "reporting",
                table: "ReportExportJobs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastHeartbeatAtUtc",
                schema: "reporting",
                table: "ReportExportJobs",
                type: "datetimeoffset(7)",
                precision: 7,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseExpiresAtUtc",
                schema: "reporting",
                table: "ReportExportJobs",
                type: "datetimeoffset(7)",
                precision: 7,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RowCount",
                schema: "reporting",
                table: "ReportExportJobs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAtUtc",
                schema: "reporting",
                table: "ReportExportJobs",
                type: "datetimeoffset(7)",
                precision: 7,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ToDate",
                schema: "reporting",
                table: "ReportExportJobs",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentLengthBytes",
                schema: "reporting",
                table: "ReportExportJobs");

            migrationBuilder.DropColumn(
                name: "FromDate",
                schema: "reporting",
                table: "ReportExportJobs");

            migrationBuilder.DropColumn(
                name: "LastHeartbeatAtUtc",
                schema: "reporting",
                table: "ReportExportJobs");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAtUtc",
                schema: "reporting",
                table: "ReportExportJobs");

            migrationBuilder.DropColumn(
                name: "RowCount",
                schema: "reporting",
                table: "ReportExportJobs");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                schema: "reporting",
                table: "ReportExportJobs");

            migrationBuilder.DropColumn(
                name: "ToDate",
                schema: "reporting",
                table: "ReportExportJobs");
        }
    }
}
