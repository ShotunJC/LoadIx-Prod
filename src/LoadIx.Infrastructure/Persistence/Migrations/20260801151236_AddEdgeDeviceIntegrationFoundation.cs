using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoadIx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEdgeDeviceIntegrationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "edge");

            migrationBuilder.AddColumn<Guid>(
                name: "ActorDeviceId",
                schema: "audit",
                table: "SecurityAuditEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                schema: "audit",
                table: "SecurityAuditEvents",
                type: "varchar(128)",
                unicode: false,
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeviceHeartbeats",
                schema: "edge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EdgeDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceTimestampUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    AgentVersion = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    Status = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceHeartbeats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EdgeCommands",
                schema: "edge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EdgeDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CredentialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommandType = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    Nonce = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    DeviceTimestampUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    CorrelationId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FailureCode = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdgeCommands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EdgeDeviceCredentials",
                schema: "edge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EdgeDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    SecretHash = table.Column<string>(type: "varchar(512)", unicode: false, maxLength: 512, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    LastUsedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    ReplacedCredentialId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdgeDeviceCredentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EdgeDevices",
                schema: "edge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Protocol = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    ParserVersion = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    RegisteredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    LastHeartbeatAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    LastAgentVersion = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    LastHeartbeatStatus = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdgeDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EdgeTicketSubmissions",
                schema: "edge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EdgeDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeighbridgeDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EdgeCommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketNumber = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CapturedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentType = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CorrelationId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdgeTicketSubmissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RawWeightReadings",
                schema: "edge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EdgeDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeighbridgeDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayedValue = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    IsStable = table.Column<bool>(type: "bit", nullable: false),
                    CapturedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    VehicleRegistration = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    SerialMessageMetadata = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ParserVersion = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    CorrelationId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    EdgeCommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawWeightReadings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StableWeightReadings",
                schema: "edge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawWeightReadingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EdgeDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeighbridgeDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayedValue = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    CapturedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    VehicleRegistration = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    CorrelationId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StableWeightReadings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeighbridgeDevices",
                schema: "edge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EdgeDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Code = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Protocol = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    ParserVersion = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeighbridgeDevices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvents_TenantId_ActorDeviceId_OccurredAtUtc",
                schema: "audit",
                table: "SecurityAuditEvents",
                columns: new[] { "TenantId", "ActorDeviceId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceHeartbeats_TenantId_EdgeDeviceId_ReceivedAtUtc_Id",
                schema: "edge",
                table: "DeviceHeartbeats",
                columns: new[] { "TenantId", "EdgeDeviceId", "ReceivedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_EdgeCommands_TenantId_EdgeDeviceId_IdempotencyKey",
                schema: "edge",
                table: "EdgeCommands",
                columns: new[] { "TenantId", "EdgeDeviceId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EdgeCommands_TenantId_EdgeDeviceId_Nonce",
                schema: "edge",
                table: "EdgeCommands",
                columns: new[] { "TenantId", "EdgeDeviceId", "Nonce" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EdgeCommands_TenantId_EdgeDeviceId_ReceivedAtUtc_Id",
                schema: "edge",
                table: "EdgeCommands",
                columns: new[] { "TenantId", "EdgeDeviceId", "ReceivedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_EdgeDeviceCredentials_TenantId_EdgeDeviceId_Type_RevokedAtUtc",
                schema: "edge",
                table: "EdgeDeviceCredentials",
                columns: new[] { "TenantId", "EdgeDeviceId", "Type", "RevokedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EdgeDevices_TenantId_Name",
                schema: "edge",
                table: "EdgeDevices",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EdgeDevices_TenantId_SiteId_Status_Id",
                schema: "edge",
                table: "EdgeDevices",
                columns: new[] { "TenantId", "SiteId", "Status", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_EdgeTicketSubmissions_TenantId_EdgeDeviceId_ReceivedAtUtc_Id",
                schema: "edge",
                table: "EdgeTicketSubmissions",
                columns: new[] { "TenantId", "EdgeDeviceId", "ReceivedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_RawWeightReadings_TenantId_EdgeDeviceId_ReceivedAtUtc_Id",
                schema: "edge",
                table: "RawWeightReadings",
                columns: new[] { "TenantId", "EdgeDeviceId", "ReceivedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_RawWeightReadings_TenantId_WeighbridgeDeviceId_ReceivedAtUtc_Id",
                schema: "edge",
                table: "RawWeightReadings",
                columns: new[] { "TenantId", "WeighbridgeDeviceId", "ReceivedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StableWeightReadings_TenantId_EdgeDeviceId_ReceivedAtUtc_Id",
                schema: "edge",
                table: "StableWeightReadings",
                columns: new[] { "TenantId", "EdgeDeviceId", "ReceivedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StableWeightReadings_TenantId_RawWeightReadingId",
                schema: "edge",
                table: "StableWeightReadings",
                columns: new[] { "TenantId", "RawWeightReadingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeighbridgeDevices_TenantId_Code",
                schema: "edge",
                table: "WeighbridgeDevices",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeighbridgeDevices_TenantId_EdgeDeviceId_SiteId_IsActive_Id",
                schema: "edge",
                table: "WeighbridgeDevices",
                columns: new[] { "TenantId", "EdgeDeviceId", "SiteId", "IsActive", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceHeartbeats",
                schema: "edge");

            migrationBuilder.DropTable(
                name: "EdgeCommands",
                schema: "edge");

            migrationBuilder.DropTable(
                name: "EdgeDeviceCredentials",
                schema: "edge");

            migrationBuilder.DropTable(
                name: "EdgeDevices",
                schema: "edge");

            migrationBuilder.DropTable(
                name: "EdgeTicketSubmissions",
                schema: "edge");

            migrationBuilder.DropTable(
                name: "RawWeightReadings",
                schema: "edge");

            migrationBuilder.DropTable(
                name: "StableWeightReadings",
                schema: "edge");

            migrationBuilder.DropTable(
                name: "WeighbridgeDevices",
                schema: "edge");

            migrationBuilder.DropIndex(
                name: "IX_SecurityAuditEvents_TenantId_ActorDeviceId_OccurredAtUtc",
                schema: "audit",
                table: "SecurityAuditEvents");

            migrationBuilder.DropColumn(
                name: "ActorDeviceId",
                schema: "audit",
                table: "SecurityAuditEvents");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                schema: "audit",
                table: "SecurityAuditEvents");
        }
    }
}
