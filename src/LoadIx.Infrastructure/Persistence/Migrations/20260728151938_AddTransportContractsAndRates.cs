using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoadIx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransportContractsAndRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Locations_TenantId_Id",
                schema: "locations",
                table: "Locations",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Commodities_TenantId_Id",
                schema: "commercial",
                table: "Commodities",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "TransportContracts",
                schema: "commercial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractNumber = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    DefaultCurrency = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    PaymentTermsDays = table.Column<int>(type: "int", nullable: false),
                    WeightTolerancePercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransportContracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransportContracts_Customers_TenantId_CustomerId",
                        columns: x => new { x.TenantId, x.CustomerId },
                        principalSchema: "customers",
                        principalTable: "Customers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransportContracts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "tenancy",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractCommodities",
                schema: "commercial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransportContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommodityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractCommodities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractCommodities_Commodities_TenantId_CommodityId",
                        columns: x => new { x.TenantId, x.CommodityId },
                        principalSchema: "commercial",
                        principalTable: "Commodities",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractCommodities_TransportContracts_TransportContractId",
                        column: x => x.TransportContractId,
                        principalSchema: "commercial",
                        principalTable: "TransportContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractRoutes",
                schema: "commercial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransportContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinationLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractRoutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractRoutes_Locations_TenantId_DestinationLocationId",
                        columns: x => new { x.TenantId, x.DestinationLocationId },
                        principalSchema: "locations",
                        principalTable: "Locations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractRoutes_Locations_TenantId_SourceLocationId",
                        columns: x => new { x.TenantId, x.SourceLocationId },
                        principalSchema: "locations",
                        principalTable: "Locations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractRoutes_TransportContracts_TransportContractId",
                        column: x => x.TransportContractId,
                        principalSchema: "commercial",
                        principalTable: "TransportContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractRates",
                schema: "commercial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransportContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractRouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RateType = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    FixedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    BaseFee = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PerTonneAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    MinimumCharge = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    FuelSurchargePercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    RoundingDecimalPlaces = table.Column<int>(type: "int", nullable: false),
                    RoundingMode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractRates_ContractRoutes_ContractRouteId",
                        column: x => x.ContractRouteId,
                        principalSchema: "commercial",
                        principalTable: "ContractRoutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractRates_TransportContracts_TransportContractId",
                        column: x => x.TransportContractId,
                        principalSchema: "commercial",
                        principalTable: "TransportContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractCommodities_TenantId_CommodityId",
                schema: "commercial",
                table: "ContractCommodities",
                columns: new[] { "TenantId", "CommodityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractCommodities_TenantId_TransportContractId_CommodityId",
                schema: "commercial",
                table: "ContractCommodities",
                columns: new[] { "TenantId", "TransportContractId", "CommodityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractCommodities_TransportContractId",
                schema: "commercial",
                table: "ContractCommodities",
                column: "TransportContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractRates_ContractRouteId",
                schema: "commercial",
                table: "ContractRates",
                column: "ContractRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractRates_TenantId_ContractRouteId_EffectiveFrom_Id",
                schema: "commercial",
                table: "ContractRates",
                columns: new[] { "TenantId", "ContractRouteId", "EffectiveFrom", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractRates_TransportContractId",
                schema: "commercial",
                table: "ContractRates",
                column: "TransportContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractRoutes_TenantId_DestinationLocationId",
                schema: "commercial",
                table: "ContractRoutes",
                columns: new[] { "TenantId", "DestinationLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractRoutes_TenantId_SourceLocationId",
                schema: "commercial",
                table: "ContractRoutes",
                columns: new[] { "TenantId", "SourceLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractRoutes_TenantId_TransportContractId_Reference",
                schema: "commercial",
                table: "ContractRoutes",
                columns: new[] { "TenantId", "TransportContractId", "Reference" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractRoutes_TransportContractId",
                schema: "commercial",
                table: "ContractRoutes",
                column: "TransportContractId");

            migrationBuilder.CreateIndex(
                name: "IX_TransportContracts_TenantId_ContractNumber",
                schema: "commercial",
                table: "TransportContracts",
                columns: new[] { "TenantId", "ContractNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransportContracts_TenantId_CustomerId_Status",
                schema: "commercial",
                table: "TransportContracts",
                columns: new[] { "TenantId", "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TransportContracts_TenantId_Status_StartDate_Id",
                schema: "commercial",
                table: "TransportContracts",
                columns: new[] { "TenantId", "Status", "StartDate", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractCommodities",
                schema: "commercial");

            migrationBuilder.DropTable(
                name: "ContractRates",
                schema: "commercial");

            migrationBuilder.DropTable(
                name: "ContractRoutes",
                schema: "commercial");

            migrationBuilder.DropTable(
                name: "TransportContracts",
                schema: "commercial");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Locations_TenantId_Id",
                schema: "locations",
                table: "Locations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Commodities_TenantId_Id",
                schema: "commercial",
                table: "Commodities");
        }
    }
}
