using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoadIx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRabbitMqReportExportJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reporting");

            migrationBuilder.CreateTable(
                name: "ReportExportJobs",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Report = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    FromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ToUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Page = table.Column<int>(type: "int", nullable: false),
                    PageSize = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    LastPublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportExportJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportJobs_Status_LastPublishedAtUtc_CreatedAtUtc",
                schema: "reporting",
                table: "ReportExportJobs",
                columns: new[] { "Status", "LastPublishedAtUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportJobs_TenantId_Id",
                schema: "reporting",
                table: "ReportExportJobs",
                columns: new[] { "TenantId", "Id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportExportJobs",
                schema: "reporting");
        }
    }
}
