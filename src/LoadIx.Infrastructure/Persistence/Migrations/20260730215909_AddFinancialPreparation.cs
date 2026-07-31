using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoadIx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialPreparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finance");

            migrationBuilder.CreateTable(
                name: "FinancialApprovals",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetType = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Outcome = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialApprovals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceBatches",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchNumber = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TransportContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractNumber = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    CurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExportedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantTaxConfigurations",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefaultTaxTreatment = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    TaxRatePercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantTaxConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TripFinancials",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripNumber = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TransportContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractNumber = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    InvoiceBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    RateType = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    RateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FixedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    BaseFee = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PerTonneAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    MinimumCharge = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    FuelSurchargePercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    BillableTonnes = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Surcharge = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    TaxExclusiveAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    TaxTreatment = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    TaxRatePercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CalculatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CalculationVersion = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripFinancials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripFinancials_Customers_TenantId_CustomerId",
                        columns: x => new { x.TenantId, x.CustomerId },
                        principalSchema: "customers",
                        principalTable: "Customers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TripFinancials_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "tenancy",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TripFinancials_Trips_TripId",
                        column: x => x.TripId,
                        principalSchema: "trips",
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceBatchLines",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripFinancialsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripNumber = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransportContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillableTonnes = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    TaxExclusiveAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    TaxTreatment = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceBatchLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceBatchLines_InvoiceBatches_InvoiceBatchId",
                        column: x => x.InvoiceBatchId,
                        principalSchema: "finance",
                        principalTable: "InvoiceBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CostEntries",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripFinancialsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsAdjustment = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostEntries_TripFinancials_TripFinancialsId",
                        column: x => x.TripFinancialsId,
                        principalSchema: "finance",
                        principalTable: "TripFinancials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_TenantId_TripFinancialsId_OccurredAtUtc_Id",
                schema: "finance",
                table: "CostEntries",
                columns: new[] { "TenantId", "TripFinancialsId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_TripFinancialsId",
                schema: "finance",
                table: "CostEntries",
                column: "TripFinancialsId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialApprovals_TenantId_TargetType_TargetId_OccurredAtUtc",
                schema: "finance",
                table: "FinancialApprovals",
                columns: new[] { "TenantId", "TargetType", "TargetId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceBatches_TenantId_BatchNumber",
                schema: "finance",
                table: "InvoiceBatches",
                columns: new[] { "TenantId", "BatchNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceBatches_TenantId_Status_CreatedAtUtc",
                schema: "finance",
                table: "InvoiceBatches",
                columns: new[] { "TenantId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceBatchLines_InvoiceBatchId",
                schema: "finance",
                table: "InvoiceBatchLines",
                column: "InvoiceBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceBatchLines_TenantId_InvoiceBatchId_TripNumber",
                schema: "finance",
                table: "InvoiceBatchLines",
                columns: new[] { "TenantId", "InvoiceBatchId", "TripNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceBatchLines_TenantId_TripFinancialsId",
                schema: "finance",
                table: "InvoiceBatchLines",
                columns: new[] { "TenantId", "TripFinancialsId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantTaxConfigurations_TenantId",
                schema: "finance",
                table: "TenantTaxConfigurations",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripFinancials_TenantId_CustomerId_TransportContractId_ApprovalStatus_InvoiceBatchId",
                schema: "finance",
                table: "TripFinancials",
                columns: new[] { "TenantId", "CustomerId", "TransportContractId", "ApprovalStatus", "InvoiceBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_TripFinancials_TenantId_InvoiceBatchId_TripId",
                schema: "finance",
                table: "TripFinancials",
                columns: new[] { "TenantId", "InvoiceBatchId", "TripId" },
                unique: true,
                filter: "[InvoiceBatchId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TripFinancials_TenantId_TripId",
                schema: "finance",
                table: "TripFinancials",
                columns: new[] { "TenantId", "TripId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripFinancials_TripId",
                schema: "finance",
                table: "TripFinancials",
                column: "TripId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CostEntries",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "FinancialApprovals",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "InvoiceBatchLines",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "TenantTaxConfigurations",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "TripFinancials",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "InvoiceBatches",
                schema: "finance");
        }
    }
}
