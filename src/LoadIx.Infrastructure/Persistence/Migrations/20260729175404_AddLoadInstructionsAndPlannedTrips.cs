using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoadIx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoadInstructionsAndPlannedTrips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "instructions");

            migrationBuilder.CreateTable(
                name: "LoadInstructions",
                schema: "instructions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstructionNumber = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransportContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractRouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommodityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    QuantityUnit = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    RequestedTripCount = table.Column<int>(type: "int", nullable: true),
                    CollectionWindowStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CollectionWindowEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeliveryWindowStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeliveryWindowEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CustomerPurchaseOrderReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CustomerInstructionReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Priority = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    SpecialInstructions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoadInstructions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoadInstructions_Commodities_TenantId_CommodityId",
                        columns: x => new { x.TenantId, x.CommodityId },
                        principalSchema: "commercial",
                        principalTable: "Commodities",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoadInstructions_ContractRoutes_ContractRouteId",
                        column: x => x.ContractRouteId,
                        principalSchema: "commercial",
                        principalTable: "ContractRoutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoadInstructions_Customers_TenantId_CustomerId",
                        columns: x => new { x.TenantId, x.CustomerId },
                        principalSchema: "customers",
                        principalTable: "Customers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoadInstructions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "tenancy",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoadInstructions_TransportContracts_TransportContractId",
                        column: x => x.TransportContractId,
                        principalSchema: "commercial",
                        principalTable: "TransportContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GeneratedTrips",
                schema: "instructions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoadInstructionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripNumber = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    PlannedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CompletedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    QuantityUnit = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedTrips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeneratedTrips_LoadInstructions_LoadInstructionId",
                        column: x => x.LoadInstructionId,
                        principalSchema: "instructions",
                        principalTable: "LoadInstructions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripGenerationOperations",
                schema: "instructions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoadInstructionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripGenerationOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripGenerationOperations_LoadInstructions_LoadInstructionId",
                        column: x => x.LoadInstructionId,
                        principalSchema: "instructions",
                        principalTable: "LoadInstructions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedTrips_LoadInstructionId",
                schema: "instructions",
                table: "GeneratedTrips",
                column: "LoadInstructionId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedTrips_TenantId_LoadInstructionId_Status_Id",
                schema: "instructions",
                table: "GeneratedTrips",
                columns: new[] { "TenantId", "LoadInstructionId", "Status", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedTrips_TenantId_TripNumber",
                schema: "instructions",
                table: "GeneratedTrips",
                columns: new[] { "TenantId", "TripNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoadInstructions_ContractRouteId",
                schema: "instructions",
                table: "LoadInstructions",
                column: "ContractRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_LoadInstructions_TenantId_CommodityId",
                schema: "instructions",
                table: "LoadInstructions",
                columns: new[] { "TenantId", "CommodityId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoadInstructions_TenantId_CustomerId_Status",
                schema: "instructions",
                table: "LoadInstructions",
                columns: new[] { "TenantId", "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LoadInstructions_TenantId_InstructionNumber",
                schema: "instructions",
                table: "LoadInstructions",
                columns: new[] { "TenantId", "InstructionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoadInstructions_TenantId_Status_CollectionWindowStartUtc_Id",
                schema: "instructions",
                table: "LoadInstructions",
                columns: new[] { "TenantId", "Status", "CollectionWindowStartUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LoadInstructions_TenantId_TransportContractId_Status",
                schema: "instructions",
                table: "LoadInstructions",
                columns: new[] { "TenantId", "TransportContractId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LoadInstructions_TransportContractId",
                schema: "instructions",
                table: "LoadInstructions",
                column: "TransportContractId");

            migrationBuilder.CreateIndex(
                name: "IX_TripGenerationOperations_LoadInstructionId",
                schema: "instructions",
                table: "TripGenerationOperations",
                column: "LoadInstructionId");

            migrationBuilder.CreateIndex(
                name: "IX_TripGenerationOperations_TenantId_LoadInstructionId_IdempotencyKey",
                schema: "instructions",
                table: "TripGenerationOperations",
                columns: new[] { "TenantId", "LoadInstructionId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeneratedTrips",
                schema: "instructions");

            migrationBuilder.DropTable(
                name: "TripGenerationOperations",
                schema: "instructions");

            migrationBuilder.DropTable(
                name: "LoadInstructions",
                schema: "instructions");
        }
    }
}
