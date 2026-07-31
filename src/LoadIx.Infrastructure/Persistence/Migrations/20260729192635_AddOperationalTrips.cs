using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoadIx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalTrips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "trips");

            migrationBuilder.CreateTable(
                name: "TripCommandOperations",
                schema: "trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Command = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripCommandOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Trips",
                schema: "trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripNumber = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    LoadInstructionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransportContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractNumber = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    ContractVersion = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ContractRouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RouteReference = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    CommodityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlannedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    QuantityUnit = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    RateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RateType = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    RateEffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    RateEffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    RateFixedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    RateBaseFee = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    RatePerTonneAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    RateMinimumCharge = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    RateFuelSurchargePercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    RateRoundingDecimalPlaces = table.Column<int>(type: "int", nullable: false),
                    RateRoundingMode = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    RateCurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TrailerIds = table.Column<string>(type: "varchar(110)", unicode: false, maxLength: 110, nullable: false),
                    PlannedCollectionAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PlannedDeliveryAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    OperationalNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CancellationOrRejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DispatchedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CollectionArrivalAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LoadingStartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CollectionCompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    InTransitAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeliveryArrivalAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UnloadingStartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeliveryCompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReconciliationSubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RejectedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SourceGrossWeight = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    SourceTareWeight = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    SourceWeighbridgeReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    DestinationGrossWeight = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    DestinationTareWeight = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    DestinationWeighbridgeReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsReconciled = table.Column<bool>(type: "bit", nullable: false),
                    HasRequiredDocuments = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trips_Commodities_TenantId_CommodityId",
                        columns: x => new { x.TenantId, x.CommodityId },
                        principalSchema: "commercial",
                        principalTable: "Commodities",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trips_ContractRoutes_ContractRouteId",
                        column: x => x.ContractRouteId,
                        principalSchema: "commercial",
                        principalTable: "ContractRoutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trips_Customers_TenantId_CustomerId",
                        columns: x => new { x.TenantId, x.CustomerId },
                        principalSchema: "customers",
                        principalTable: "Customers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trips_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalSchema: "fleet",
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trips_LoadInstructions_LoadInstructionId",
                        column: x => x.LoadInstructionId,
                        principalSchema: "instructions",
                        principalTable: "LoadInstructions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trips_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "tenancy",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trips_TransportContracts_TransportContractId",
                        column: x => x.TransportContractId,
                        principalSchema: "commercial",
                        principalTable: "TransportContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trips_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalSchema: "fleet",
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TripAssignments",
                schema: "trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrailerIds = table.Column<string>(type: "varchar(110)", unicode: false, maxLength: 110, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripAssignments_Trips_TripId",
                        column: x => x.TripId,
                        principalSchema: "trips",
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripTransitions",
                schema: "trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousStatus = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    NewStatus = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsManualOverride = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripTransitions_Trips_TripId",
                        column: x => x.TripId,
                        principalSchema: "trips",
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TripAssignments_TenantId_TripId_AssignedAtUtc_Id",
                schema: "trips",
                table: "TripAssignments",
                columns: new[] { "TenantId", "TripId", "AssignedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TripAssignments_TripId",
                schema: "trips",
                table: "TripAssignments",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_TripCommandOperations_TenantId_ActorUserId_IdempotencyKey",
                schema: "trips",
                table: "TripCommandOperations",
                columns: new[] { "TenantId", "ActorUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_ContractRouteId",
                schema: "trips",
                table: "Trips",
                column: "ContractRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_DriverId",
                schema: "trips",
                table: "Trips",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_LoadInstructionId",
                schema: "trips",
                table: "Trips",
                column: "LoadInstructionId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TenantId_CommodityId",
                schema: "trips",
                table: "Trips",
                columns: new[] { "TenantId", "CommodityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TenantId_CustomerId",
                schema: "trips",
                table: "Trips",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TenantId_DriverId_PlannedCollectionAtUtc_PlannedDeliveryAtUtc",
                schema: "trips",
                table: "Trips",
                columns: new[] { "TenantId", "DriverId", "PlannedCollectionAtUtc", "PlannedDeliveryAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TenantId_LoadInstructionId",
                schema: "trips",
                table: "Trips",
                columns: new[] { "TenantId", "LoadInstructionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TenantId_Status_PlannedCollectionAtUtc_Id",
                schema: "trips",
                table: "Trips",
                columns: new[] { "TenantId", "Status", "PlannedCollectionAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TenantId_TripNumber",
                schema: "trips",
                table: "Trips",
                columns: new[] { "TenantId", "TripNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TenantId_VehicleId_PlannedCollectionAtUtc_PlannedDeliveryAtUtc",
                schema: "trips",
                table: "Trips",
                columns: new[] { "TenantId", "VehicleId", "PlannedCollectionAtUtc", "PlannedDeliveryAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TransportContractId",
                schema: "trips",
                table: "Trips",
                column: "TransportContractId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_VehicleId",
                schema: "trips",
                table: "Trips",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_TripTransitions_TenantId_TripId_OccurredAtUtc_Id",
                schema: "trips",
                table: "TripTransitions",
                columns: new[] { "TenantId", "TripId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TripTransitions_TripId",
                schema: "trips",
                table: "TripTransitions",
                column: "TripId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TripAssignments",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "TripCommandOperations",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "TripTransitions",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "Trips",
                schema: "trips");
        }
    }
}
