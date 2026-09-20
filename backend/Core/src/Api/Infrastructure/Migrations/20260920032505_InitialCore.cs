using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CashFlow.Core.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    CorrelationId = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxEvents",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxEvents", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "CreationAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Intent = table.Column<string>(type: "text", nullable: false),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreationAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreationAttempts_LedgerEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "LedgerEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreationAttempts_ActorId_Key",
                table: "CreationAttempts",
                columns: new[] { "ActorId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreationAttempts_EntryId",
                table: "CreationAttempts",
                column: "EntryId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxEvents_PublishedAt",
                table: "OutboxEvents",
                column: "PublishedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditRecords");

            migrationBuilder.DropTable(
                name: "CreationAttempts");

            migrationBuilder.DropTable(
                name: "OutboxEvents");

            migrationBuilder.DropTable(
                name: "LedgerEntries");
        }
    }
}
