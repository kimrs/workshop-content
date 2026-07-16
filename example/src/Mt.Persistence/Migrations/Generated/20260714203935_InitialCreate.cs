using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mt.Persistence.Migrations.Generated
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Inbox",
                columns: table => new
                {
                    MigrationId = table.Column<long>(type: "bigint", nullable: false),
                    DomainEvent = table.Column<string>(type: "text", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inbox", x => new { x.MigrationId, x.DomainEvent, x.Attempt });
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationId = table.Column<long>(type: "bigint", nullable: false),
                    DomainEvent = table.Column<string>(type: "text", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    TraceParent = table.Column<string>(type: "text", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Migrations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    State = table.Column<int>(type: "integer", nullable: false),
                    OrganizationNumber = table.Column<string>(type: "text", nullable: false),
                    SourceLocked = table.Column<bool>(type: "boolean", nullable: false),
                    TargetLocked = table.Column<bool>(type: "boolean", nullable: false),
                    ExportTriggered = table.Column<bool>(type: "boolean", nullable: false),
                    SourceUnlocked = table.Column<bool>(type: "boolean", nullable: false),
                    TargetUnlocked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Migrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationId = table.Column<long>(type: "bigint", nullable: false),
                    DomainEvent = table.Column<string>(type: "text", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    TraceParent = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationId = table.Column<long>(type: "bigint", nullable: false),
                    DomainEvent = table.Column<string>(type: "text", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    TraceParent = table.Column<string>(type: "text", nullable: true),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalIds",
                columns: table => new
                {
                    MigrationId = table.Column<long>(type: "bigint", nullable: false),
                    System = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalIds", x => new { x.MigrationId, x.System, x.Name });
                    table.ForeignKey(
                        name: "FK_ExternalIds_Migrations_MigrationId",
                        column: x => x.MigrationId,
                        principalTable: "Migrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIds_System_Name_Value",
                table: "ExternalIds",
                columns: new[] { "System", "Name", "Value" },
                unique: true,
                filter: "NOT \"IsCancelled\"");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConsumedAt",
                table: "Messages",
                column: "ConsumedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Migrations_OrganizationNumber",
                table: "Migrations",
                column: "OrganizationNumber",
                unique: true,
                filter: "\"State\" NOT IN (6, 8)");

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_ProcessedAt_FailedAt",
                table: "Outbox",
                columns: new[] { "ProcessedAt", "FailedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledEvents_ProcessedAt_ScheduledAt",
                table: "ScheduledEvents",
                columns: new[] { "ProcessedAt", "ScheduledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalIds");

            migrationBuilder.DropTable(
                name: "Inbox");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Outbox");

            migrationBuilder.DropTable(
                name: "ScheduledEvents");

            migrationBuilder.DropTable(
                name: "Migrations");
        }
    }
}
