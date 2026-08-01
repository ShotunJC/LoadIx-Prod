using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoadIx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceOneActiveEdgeCredential : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_EdgeDeviceCredentials_TenantId_EdgeDeviceId_Type",
                schema: "edge",
                table: "EdgeDeviceCredentials",
                columns: new[] { "TenantId", "EdgeDeviceId", "Type" },
                unique: true,
                filter: "[RevokedAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EdgeDeviceCredentials_TenantId_EdgeDeviceId_Type",
                schema: "edge",
                table: "EdgeDeviceCredentials");
        }
    }
}
