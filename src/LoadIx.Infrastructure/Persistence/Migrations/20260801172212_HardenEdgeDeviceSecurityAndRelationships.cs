using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoadIx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenEdgeDeviceSecurityAndRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EdgeDeviceCredentials_TenantId_EdgeDeviceId_Type",
                schema: "edge",
                table: "EdgeDeviceCredentials");

            migrationBuilder.AddColumn<Guid>(
                name: "ActorCredentialId",
                schema: "audit",
                table: "SecurityAuditEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                schema: "audit",
                table: "SecurityAuditEvents",
                type: "varchar(160)",
                unicode: false,
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKeyHash",
                schema: "audit",
                table: "SecurityAuditEvents",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NonceHash",
                schema: "audit",
                table: "SecurityAuditEvents",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceId",
                schema: "audit",
                table: "SecurityAuditEvents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActivatedAtUtc",
                schema: "edge",
                table: "EdgeDeviceCredentials",
                type: "datetimeoffset(7)",
                precision: 7,
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmedAtUtc",
                schema: "edge",
                table: "EdgeDeviceCredentials",
                type: "datetimeoffset(7)",
                precision: 7,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OverlapExpiresAtUtc",
                schema: "edge",
                table: "EdgeDeviceCredentials",
                type: "datetimeoffset(7)",
                precision: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "edge",
                table: "EdgeDeviceCredentials",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE [edge].[EdgeDeviceCredentials] " +
                "SET [Status] = 'Active', [ActivatedAtUtc] = [CreatedAtUtc], " +
                "[ConfirmedAtUtc] = CASE WHEN [Type] = 'Device' " +
                "THEN [CreatedAtUtc] ELSE NULL END " +
                "WHERE [Status] = '';");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_WeighbridgeDevices_TenantId_Id",
                schema: "edge",
                table: "WeighbridgeDevices",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_RawWeightReadings_TenantId_Id",
                schema: "edge",
                table: "RawWeightReadings",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EdgeDevices_TenantId_Id",
                schema: "edge",
                table: "EdgeDevices",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EdgeDeviceCredentials_TenantId_Id",
                schema: "edge",
                table: "EdgeDeviceCredentials",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EdgeCommands_TenantId_Id",
                schema: "edge",
                table: "EdgeCommands",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WeighbridgeDevices_TenantId_SiteId",
                schema: "edge",
                table: "WeighbridgeDevices",
                columns: new[] { "TenantId", "SiteId" });

            migrationBuilder.CreateIndex(
                name: "IX_StableWeightReadings_TenantId_WeighbridgeDeviceId",
                schema: "edge",
                table: "StableWeightReadings",
                columns: new[] { "TenantId", "WeighbridgeDeviceId" });

            migrationBuilder.CreateIndex(
                name: "IX_RawWeightReadings_TenantId_EdgeCommandId",
                schema: "edge",
                table: "RawWeightReadings",
                columns: new[] { "TenantId", "EdgeCommandId" });

            migrationBuilder.CreateIndex(
                name: "IX_EdgeTicketSubmissions_TenantId_EdgeCommandId",
                schema: "edge",
                table: "EdgeTicketSubmissions",
                columns: new[] { "TenantId", "EdgeCommandId" });

            migrationBuilder.CreateIndex(
                name: "IX_EdgeTicketSubmissions_TenantId_WeighbridgeDeviceId",
                schema: "edge",
                table: "EdgeTicketSubmissions",
                columns: new[] { "TenantId", "WeighbridgeDeviceId" });

            migrationBuilder.CreateIndex(
                name: "IX_EdgeDeviceCredentials_TenantId_EdgeDeviceId_Type",
                schema: "edge",
                table: "EdgeDeviceCredentials",
                columns: new[] { "TenantId", "EdgeDeviceId", "Type" },
                unique: true,
                filter: "[RevokedAtUtc] IS NULL AND [Type] = 'Bootstrap'");

            migrationBuilder.CreateIndex(
                name: "IX_EdgeDeviceCredentials_TenantId_EdgeDeviceId_Type_Status",
                schema: "edge",
                table: "EdgeDeviceCredentials",
                columns: new[] { "TenantId", "EdgeDeviceId", "Type", "Status" },
                unique: true,
                filter: "[RevokedAtUtc] IS NULL AND [Status] = 'PendingConfirmation'");

            migrationBuilder.CreateIndex(
                name: "IX_EdgeDeviceCredentials_TenantId_ReplacedCredentialId",
                schema: "edge",
                table: "EdgeDeviceCredentials",
                columns: new[] { "TenantId", "ReplacedCredentialId" });

            migrationBuilder.CreateIndex(
                name: "IX_EdgeCommands_TenantId_CredentialId",
                schema: "edge",
                table: "EdgeCommands",
                columns: new[] { "TenantId", "CredentialId" });

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceHeartbeats_EdgeDevices_TenantId_EdgeDeviceId",
                schema: "edge",
                table: "DeviceHeartbeats",
                columns: new[] { "TenantId", "EdgeDeviceId" },
                principalSchema: "edge",
                principalTable: "EdgeDevices",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceHeartbeats_Tenants_TenantId",
                schema: "edge",
                table: "DeviceHeartbeats",
                column: "TenantId",
                principalSchema: "tenancy",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EdgeCommands_EdgeDeviceCredentials_TenantId_CredentialId",
                schema: "edge",
                table: "EdgeCommands",
                columns: new[] { "TenantId", "CredentialId" },
                principalSchema: "edge",
                principalTable: "EdgeDeviceCredentials",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EdgeCommands_EdgeDevices_TenantId_EdgeDeviceId",
                schema: "edge",
                table: "EdgeCommands",
                columns: new[] { "TenantId", "EdgeDeviceId" },
                principalSchema: "edge",
                principalTable: "EdgeDevices",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EdgeCommands_Tenants_TenantId",
                schema: "edge",
                table: "EdgeCommands",
                column: "TenantId",
                principalSchema: "tenancy",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EdgeDeviceCredentials_EdgeDeviceCredentials_TenantId_ReplacedCredentialId",
                schema: "edge",
                table: "EdgeDeviceCredentials",
                columns: new[] { "TenantId", "ReplacedCredentialId" },
                principalSchema: "edge",
                principalTable: "EdgeDeviceCredentials",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EdgeDeviceCredentials_EdgeDevices_TenantId_EdgeDeviceId",
                schema: "edge",
                table: "EdgeDeviceCredentials",
                columns: new[] { "TenantId", "EdgeDeviceId" },
                principalSchema: "edge",
                principalTable: "EdgeDevices",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EdgeDeviceCredentials_Tenants_TenantId",
                schema: "edge",
                table: "EdgeDeviceCredentials",
                column: "TenantId",
                principalSchema: "tenancy",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EdgeDevices_Locations_TenantId_SiteId",
                schema: "edge",
                table: "EdgeDevices",
                columns: new[] { "TenantId", "SiteId" },
                principalSchema: "locations",
                principalTable: "Locations",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EdgeDevices_Tenants_TenantId",
                schema: "edge",
                table: "EdgeDevices",
                column: "TenantId",
                principalSchema: "tenancy",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EdgeTicketSubmissions_EdgeCommands_TenantId_EdgeCommandId",
                schema: "edge",
                table: "EdgeTicketSubmissions",
                columns: new[] { "TenantId", "EdgeCommandId" },
                principalSchema: "edge",
                principalTable: "EdgeCommands",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EdgeTicketSubmissions_EdgeDevices_TenantId_EdgeDeviceId",
                schema: "edge",
                table: "EdgeTicketSubmissions",
                columns: new[] { "TenantId", "EdgeDeviceId" },
                principalSchema: "edge",
                principalTable: "EdgeDevices",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EdgeTicketSubmissions_Tenants_TenantId",
                schema: "edge",
                table: "EdgeTicketSubmissions",
                column: "TenantId",
                principalSchema: "tenancy",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EdgeTicketSubmissions_WeighbridgeDevices_TenantId_WeighbridgeDeviceId",
                schema: "edge",
                table: "EdgeTicketSubmissions",
                columns: new[] { "TenantId", "WeighbridgeDeviceId" },
                principalSchema: "edge",
                principalTable: "WeighbridgeDevices",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RawWeightReadings_EdgeCommands_TenantId_EdgeCommandId",
                schema: "edge",
                table: "RawWeightReadings",
                columns: new[] { "TenantId", "EdgeCommandId" },
                principalSchema: "edge",
                principalTable: "EdgeCommands",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RawWeightReadings_EdgeDevices_TenantId_EdgeDeviceId",
                schema: "edge",
                table: "RawWeightReadings",
                columns: new[] { "TenantId", "EdgeDeviceId" },
                principalSchema: "edge",
                principalTable: "EdgeDevices",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RawWeightReadings_Tenants_TenantId",
                schema: "edge",
                table: "RawWeightReadings",
                column: "TenantId",
                principalSchema: "tenancy",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RawWeightReadings_WeighbridgeDevices_TenantId_WeighbridgeDeviceId",
                schema: "edge",
                table: "RawWeightReadings",
                columns: new[] { "TenantId", "WeighbridgeDeviceId" },
                principalSchema: "edge",
                principalTable: "WeighbridgeDevices",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StableWeightReadings_EdgeDevices_TenantId_EdgeDeviceId",
                schema: "edge",
                table: "StableWeightReadings",
                columns: new[] { "TenantId", "EdgeDeviceId" },
                principalSchema: "edge",
                principalTable: "EdgeDevices",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StableWeightReadings_RawWeightReadings_TenantId_RawWeightReadingId",
                schema: "edge",
                table: "StableWeightReadings",
                columns: new[] { "TenantId", "RawWeightReadingId" },
                principalSchema: "edge",
                principalTable: "RawWeightReadings",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StableWeightReadings_Tenants_TenantId",
                schema: "edge",
                table: "StableWeightReadings",
                column: "TenantId",
                principalSchema: "tenancy",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StableWeightReadings_WeighbridgeDevices_TenantId_WeighbridgeDeviceId",
                schema: "edge",
                table: "StableWeightReadings",
                columns: new[] { "TenantId", "WeighbridgeDeviceId" },
                principalSchema: "edge",
                principalTable: "WeighbridgeDevices",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeighbridgeDevices_EdgeDevices_TenantId_EdgeDeviceId",
                schema: "edge",
                table: "WeighbridgeDevices",
                columns: new[] { "TenantId", "EdgeDeviceId" },
                principalSchema: "edge",
                principalTable: "EdgeDevices",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeighbridgeDevices_Locations_TenantId_SiteId",
                schema: "edge",
                table: "WeighbridgeDevices",
                columns: new[] { "TenantId", "SiteId" },
                principalSchema: "locations",
                principalTable: "Locations",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeighbridgeDevices_Tenants_TenantId",
                schema: "edge",
                table: "WeighbridgeDevices",
                column: "TenantId",
                principalSchema: "tenancy",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeviceHeartbeats_EdgeDevices_TenantId_EdgeDeviceId",
                schema: "edge",
                table: "DeviceHeartbeats");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceHeartbeats_Tenants_TenantId",
                schema: "edge",
                table: "DeviceHeartbeats");

            migrationBuilder.DropForeignKey(
                name: "FK_EdgeCommands_EdgeDeviceCredentials_TenantId_CredentialId",
                schema: "edge",
                table: "EdgeCommands");

            migrationBuilder.DropForeignKey(
                name: "FK_EdgeCommands_EdgeDevices_TenantId_EdgeDeviceId",
                schema: "edge",
                table: "EdgeCommands");

            migrationBuilder.DropForeignKey(
                name: "FK_EdgeCommands_Tenants_TenantId",
                schema: "edge",
                table: "EdgeCommands");

            migrationBuilder.DropForeignKey(
                name: "FK_EdgeDeviceCredentials_EdgeDeviceCredentials_TenantId_ReplacedCredentialId",
                schema: "edge",
                table: "EdgeDeviceCredentials");

            migrationBuilder.DropForeignKey(
                name: "FK_EdgeDeviceCredentials_EdgeDevices_TenantId_EdgeDeviceId",
                schema: "edge",
                table: "EdgeDeviceCredentials");

            migrationBuilder.DropForeignKey(
                name: "FK_EdgeDeviceCredentials_Tenants_TenantId",
                schema: "edge",
                table: "EdgeDeviceCredentials");

            migrationBuilder.DropForeignKey(
                name: "FK_EdgeDevices_Locations_TenantId_SiteId",
                schema: "edge",
                table: "EdgeDevices");

            migrationBuilder.DropForeignKey(
                name: "FK_EdgeDevices_Tenants_TenantId",
                schema: "edge",
                table: "EdgeDevices");

            migrationBuilder.DropForeignKey(
                name: "FK_EdgeTicketSubmissions_EdgeCommands_TenantId_EdgeCommandId",
                schema: "edge",
                table: "EdgeTicketSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_EdgeTicketSubmissions_EdgeDevices_TenantId_EdgeDeviceId",
                schema: "edge",
                table: "EdgeTicketSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_EdgeTicketSubmissions_Tenants_TenantId",
                schema: "edge",
                table: "EdgeTicketSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_EdgeTicketSubmissions_WeighbridgeDevices_TenantId_WeighbridgeDeviceId",
                schema: "edge",
                table: "EdgeTicketSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_RawWeightReadings_EdgeCommands_TenantId_EdgeCommandId",
                schema: "edge",
                table: "RawWeightReadings");

            migrationBuilder.DropForeignKey(
                name: "FK_RawWeightReadings_EdgeDevices_TenantId_EdgeDeviceId",
                schema: "edge",
                table: "RawWeightReadings");

            migrationBuilder.DropForeignKey(
                name: "FK_RawWeightReadings_Tenants_TenantId",
                schema: "edge",
                table: "RawWeightReadings");

            migrationBuilder.DropForeignKey(
                name: "FK_RawWeightReadings_WeighbridgeDevices_TenantId_WeighbridgeDeviceId",
                schema: "edge",
                table: "RawWeightReadings");

            migrationBuilder.DropForeignKey(
                name: "FK_StableWeightReadings_EdgeDevices_TenantId_EdgeDeviceId",
                schema: "edge",
                table: "StableWeightReadings");

            migrationBuilder.DropForeignKey(
                name: "FK_StableWeightReadings_RawWeightReadings_TenantId_RawWeightReadingId",
                schema: "edge",
                table: "StableWeightReadings");

            migrationBuilder.DropForeignKey(
                name: "FK_StableWeightReadings_Tenants_TenantId",
                schema: "edge",
                table: "StableWeightReadings");

            migrationBuilder.DropForeignKey(
                name: "FK_StableWeightReadings_WeighbridgeDevices_TenantId_WeighbridgeDeviceId",
                schema: "edge",
                table: "StableWeightReadings");

            migrationBuilder.DropForeignKey(
                name: "FK_WeighbridgeDevices_EdgeDevices_TenantId_EdgeDeviceId",
                schema: "edge",
                table: "WeighbridgeDevices");

            migrationBuilder.DropForeignKey(
                name: "FK_WeighbridgeDevices_Locations_TenantId_SiteId",
                schema: "edge",
                table: "WeighbridgeDevices");

            migrationBuilder.DropForeignKey(
                name: "FK_WeighbridgeDevices_Tenants_TenantId",
                schema: "edge",
                table: "WeighbridgeDevices");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_WeighbridgeDevices_TenantId_Id",
                schema: "edge",
                table: "WeighbridgeDevices");

            migrationBuilder.DropIndex(
                name: "IX_WeighbridgeDevices_TenantId_SiteId",
                schema: "edge",
                table: "WeighbridgeDevices");

            migrationBuilder.DropIndex(
                name: "IX_StableWeightReadings_TenantId_WeighbridgeDeviceId",
                schema: "edge",
                table: "StableWeightReadings");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_RawWeightReadings_TenantId_Id",
                schema: "edge",
                table: "RawWeightReadings");

            migrationBuilder.DropIndex(
                name: "IX_RawWeightReadings_TenantId_EdgeCommandId",
                schema: "edge",
                table: "RawWeightReadings");

            migrationBuilder.DropIndex(
                name: "IX_EdgeTicketSubmissions_TenantId_EdgeCommandId",
                schema: "edge",
                table: "EdgeTicketSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_EdgeTicketSubmissions_TenantId_WeighbridgeDeviceId",
                schema: "edge",
                table: "EdgeTicketSubmissions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EdgeDevices_TenantId_Id",
                schema: "edge",
                table: "EdgeDevices");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EdgeDeviceCredentials_TenantId_Id",
                schema: "edge",
                table: "EdgeDeviceCredentials");

            migrationBuilder.DropIndex(
                name: "IX_EdgeDeviceCredentials_TenantId_EdgeDeviceId_Type",
                schema: "edge",
                table: "EdgeDeviceCredentials");

            migrationBuilder.DropIndex(
                name: "IX_EdgeDeviceCredentials_TenantId_EdgeDeviceId_Type_Status",
                schema: "edge",
                table: "EdgeDeviceCredentials");

            migrationBuilder.DropIndex(
                name: "IX_EdgeDeviceCredentials_TenantId_ReplacedCredentialId",
                schema: "edge",
                table: "EdgeDeviceCredentials");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EdgeCommands_TenantId_Id",
                schema: "edge",
                table: "EdgeCommands");

            migrationBuilder.DropIndex(
                name: "IX_EdgeCommands_TenantId_CredentialId",
                schema: "edge",
                table: "EdgeCommands");

            migrationBuilder.DropColumn(
                name: "ActorCredentialId",
                schema: "audit",
                table: "SecurityAuditEvents");

            migrationBuilder.DropColumn(
                name: "FailureCode",
                schema: "audit",
                table: "SecurityAuditEvents");

            migrationBuilder.DropColumn(
                name: "IdempotencyKeyHash",
                schema: "audit",
                table: "SecurityAuditEvents");

            migrationBuilder.DropColumn(
                name: "NonceHash",
                schema: "audit",
                table: "SecurityAuditEvents");

            migrationBuilder.DropColumn(
                name: "TraceId",
                schema: "audit",
                table: "SecurityAuditEvents");

            migrationBuilder.DropColumn(
                name: "ActivatedAtUtc",
                schema: "edge",
                table: "EdgeDeviceCredentials");

            migrationBuilder.DropColumn(
                name: "ConfirmedAtUtc",
                schema: "edge",
                table: "EdgeDeviceCredentials");

            migrationBuilder.DropColumn(
                name: "OverlapExpiresAtUtc",
                schema: "edge",
                table: "EdgeDeviceCredentials");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "edge",
                table: "EdgeDeviceCredentials");

            migrationBuilder.CreateIndex(
                name: "IX_EdgeDeviceCredentials_TenantId_EdgeDeviceId_Type",
                schema: "edge",
                table: "EdgeDeviceCredentials",
                columns: new[] { "TenantId", "EdgeDeviceId", "Type" },
                unique: true,
                filter: "[RevokedAtUtc] IS NULL");
        }
    }
}
