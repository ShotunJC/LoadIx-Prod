using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoadIx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterDataManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_TenantId_BranchId_IsActive",
                schema: "fleet",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Trailers_TenantId_BranchId_IsActive",
                schema: "fleet",
                table: "Trailers");

            migrationBuilder.AddColumn<bool>(
                name: "IsAvailable",
                schema: "fleet",
                table: "Vehicles",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAvailable",
                schema: "fleet",
                table: "Trailers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                schema: "locations",
                table: "Locations",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhoneNumber",
                schema: "locations",
                table: "Locations",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatingHours",
                schema: "locations",
                table: "Locations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "customers",
                table: "Customers",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_TenantId_BranchId_IsActive_IsAvailable",
                schema: "fleet",
                table: "Vehicles",
                columns: new[] { "TenantId", "BranchId", "IsActive", "IsAvailable" });

            migrationBuilder.CreateIndex(
                name: "IX_Trailers_TenantId_BranchId_IsActive_IsAvailable",
                schema: "fleet",
                table: "Trailers",
                columns: new[] { "TenantId", "BranchId", "IsActive", "IsAvailable" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_TenantId_BranchId_IsActive_IsAvailable",
                schema: "fleet",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Trailers_TenantId_BranchId_IsActive_IsAvailable",
                schema: "fleet",
                table: "Trailers");

            migrationBuilder.DropColumn(
                name: "IsAvailable",
                schema: "fleet",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "IsAvailable",
                schema: "fleet",
                table: "Trailers");

            migrationBuilder.DropColumn(
                name: "ContactName",
                schema: "locations",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "ContactPhoneNumber",
                schema: "locations",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "OperatingHours",
                schema: "locations",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "customers",
                table: "Customers");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_TenantId_BranchId_IsActive",
                schema: "fleet",
                table: "Vehicles",
                columns: new[] { "TenantId", "BranchId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Trailers_TenantId_BranchId_IsActive",
                schema: "fleet",
                table: "Trailers",
                columns: new[] { "TenantId", "BranchId", "IsActive" });
        }
    }
}
