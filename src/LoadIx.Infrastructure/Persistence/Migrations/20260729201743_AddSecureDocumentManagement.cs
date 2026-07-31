using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoadIx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecureDocumentManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "documents");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                schema: "fleet",
                table: "Drivers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Documents",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerType = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "varchar(48)", unicode: false, maxLength: 48, nullable: false),
                    DisplayFileName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Checksum = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    StorageKey = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    PreviewStorageKey = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    UploaderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "tenancy",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UploadOperations",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadOperations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_TenantId_UserId",
                schema: "fleet",
                table: "Drivers",
                columns: new[] { "TenantId", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_TenantId_OwnerType_OwnerId_DocumentType_Sha256Checksum",
                schema: "documents",
                table: "Documents",
                columns: new[] { "TenantId", "OwnerType", "OwnerId", "DocumentType", "Sha256Checksum" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_TenantId_OwnerType_OwnerId_Status_UploadedAtUtc_Id",
                schema: "documents",
                table: "Documents",
                columns: new[] { "TenantId", "OwnerType", "OwnerId", "Status", "UploadedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_TenantId_StorageKey",
                schema: "documents",
                table: "Documents",
                columns: new[] { "TenantId", "StorageKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UploadOperations_TenantId_ActorUserId_IdempotencyKey",
                schema: "documents",
                table: "UploadOperations",
                columns: new[] { "TenantId", "ActorUserId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Documents",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "UploadOperations",
                schema: "documents");

            migrationBuilder.DropIndex(
                name: "IX_Drivers_TenantId_UserId",
                schema: "fleet",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "fleet",
                table: "Drivers");
        }
    }
}
