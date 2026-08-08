using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoadIx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowWeightCaptureOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DestinationWeightCaptureRequired",
                schema: "trips",
                table: "Trips",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ManualQuantity",
                schema: "trips",
                table: "Trips",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ManualQuantityPermitted",
                schema: "trips",
                table: "Trips",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "QuantitySource",
                schema: "trips",
                table: "Trips",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<decimal>(
                name: "ReconciledQuantity",
                schema: "trips",
                table: "Trips",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReconciliationStatus",
                schema: "trips",
                table: "Trips",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<bool>(
                name: "SourceWeightCaptureRequired",
                schema: "trips",
                table: "Trips",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightTolerancePercent",
                schema: "trips",
                table: "Trips",
                type: "decimal(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "ManualQuantityPermitted",
                schema: "commercial",
                table: "TransportContracts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CollectionInstructions",
                schema: "commercial",
                table: "ContractRoutes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryInstructions",
                schema: "commercial",
                table: "ContractRoutes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DestinationWeightCaptureRequired",
                schema: "commercial",
                table: "ContractRoutes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(
                """
                UPDATE trip
                SET trip.WeightTolerancePercent = contract.WeightTolerancePercent,
                    trip.ReconciliationStatus = CASE
                        WHEN trip.IsReconciled = 1 THEN 'ApprovedException'
                        ELSE 'Pending'
                    END,
                    trip.QuantitySource = CASE
                        WHEN trip.RateType = 'FixedPerTrip' THEN 'Fixed'
                        WHEN trip.IsReconciled = 1 THEN 'ReconciledMeasured'
                        ELSE 'Pending'
                    END
                FROM trips.Trips AS trip
                INNER JOIN commercial.TransportContracts AS contract
                    ON contract.Id = trip.TransportContractId
                    AND contract.TenantId = trip.TenantId;
                """);

            migrationBuilder.AddColumn<bool>(
                name: "SourceWeightCaptureRequired",
                schema: "commercial",
                table: "ContractRoutes",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DestinationWeightCaptureRequired",
                schema: "trips",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ManualQuantity",
                schema: "trips",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ManualQuantityPermitted",
                schema: "trips",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "QuantitySource",
                schema: "trips",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ReconciledQuantity",
                schema: "trips",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ReconciliationStatus",
                schema: "trips",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "SourceWeightCaptureRequired",
                schema: "trips",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "WeightTolerancePercent",
                schema: "trips",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ManualQuantityPermitted",
                schema: "commercial",
                table: "TransportContracts");

            migrationBuilder.DropColumn(
                name: "CollectionInstructions",
                schema: "commercial",
                table: "ContractRoutes");

            migrationBuilder.DropColumn(
                name: "DeliveryInstructions",
                schema: "commercial",
                table: "ContractRoutes");

            migrationBuilder.DropColumn(
                name: "DestinationWeightCaptureRequired",
                schema: "commercial",
                table: "ContractRoutes");

            migrationBuilder.DropColumn(
                name: "SourceWeightCaptureRequired",
                schema: "commercial",
                table: "ContractRoutes");
        }
    }
}
