using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoadIx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "operations");

            migrationBuilder.CreateTable(
                name: "OperationalOutboxMessages",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    DispatchedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalOutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalOutboxMessages_DispatchedAtUtc_OccurredAtUtc_Id",
                schema: "operations",
                table: "OperationalOutboxMessages",
                columns: new[] { "DispatchedAtUtc", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalOutboxMessages_TenantId_TripId_OccurredAtUtc",
                schema: "operations",
                table: "OperationalOutboxMessages",
                columns: new[] { "TenantId", "TripId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationalOutboxMessages",
                schema: "operations");
        }
    }
}
