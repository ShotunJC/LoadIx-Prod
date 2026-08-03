using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoadIx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Milestone16SecurityAndGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "governance");

            migrationBuilder.AddColumn<string>(
                name: "AfterSummaryJson",
                schema: "audit",
                table: "SecurityAuditEvents",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BeforeSummaryJson",
                schema: "audit",
                table: "SecurityAuditEvents",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                schema: "audit",
                table: "SecurityAuditEvents",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestMetadataJson",
                schema: "audit",
                table: "SecurityAuditEvents",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnonymisationRequests",
                schema: "governance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnonymisationRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantRetentionPolicies",
                schema: "governance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationalDataRetentionDays = table.Column<int>(type: "int", nullable: false),
                    AuditRetentionDays = table.Column<int>(type: "int", nullable: false),
                    DocumentMetadataRetentionDays = table.Column<int>(type: "int", nullable: false),
                    LegalHold = table.Column<bool>(type: "bit", nullable: false),
                    LegalHoldReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantRetentionPolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnonymisationRequests_TenantId_UserId_Status",
                schema: "governance",
                table: "AnonymisationRequests",
                columns: new[] { "TenantId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantRetentionPolicies_TenantId",
                schema: "governance",
                table: "TenantRetentionPolicies",
                column: "TenantId",
                unique: true);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[audit].[TR_SecurityAuditEvents_Immutable]', N'TR') IS NOT NULL
                    DROP TRIGGER [audit].[TR_SecurityAuditEvents_Immutable];
                EXEC(N'CREATE TRIGGER [audit].[TR_SecurityAuditEvents_Immutable]
                    ON [audit].[SecurityAuditEvents]
                    AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, ''Security audit events are immutable.'', 1;
                END');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[audit].[TR_SecurityAuditEvents_Immutable]', N'TR') IS NOT NULL
                    DROP TRIGGER [audit].[TR_SecurityAuditEvents_Immutable];
                """);

            migrationBuilder.DropTable(
                name: "AnonymisationRequests",
                schema: "governance");

            migrationBuilder.DropTable(
                name: "TenantRetentionPolicies",
                schema: "governance");

            migrationBuilder.DropColumn(
                name: "AfterSummaryJson",
                schema: "audit",
                table: "SecurityAuditEvents");

            migrationBuilder.DropColumn(
                name: "BeforeSummaryJson",
                schema: "audit",
                table: "SecurityAuditEvents");

            migrationBuilder.DropColumn(
                name: "Reason",
                schema: "audit",
                table: "SecurityAuditEvents");

            migrationBuilder.DropColumn(
                name: "RequestMetadataJson",
                schema: "audit",
                table: "SecurityAuditEvents");
        }
    }
}
