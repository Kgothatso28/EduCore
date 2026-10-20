using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduCore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlatformAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TargetId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformLoginEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountIdentifier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformLoginEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAuditEvents_Action_CreatedUtc",
                table: "PlatformAuditEvents",
                columns: new[] { "Action", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAuditEvents_CreatedUtc",
                table: "PlatformAuditEvents",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformLoginEvents_AccountIdentifier_CreatedUtc",
                table: "PlatformLoginEvents",
                columns: new[] { "AccountIdentifier", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformLoginEvents_CreatedUtc",
                table: "PlatformLoginEvents",
                column: "CreatedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformAuditEvents");

            migrationBuilder.DropTable(
                name: "PlatformLoginEvents");
        }
    }
}
