using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduCore.Api.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>Adds persistent credential storage for local demo authentication.</summary>
    public partial class DemoAuthentication : Migration
    {
        /// <summary>Creates demo credentials with unique identity and normalized username indexes.</summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DemoCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoCredentials", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DemoCredentials_NormalizedUserName",
                table: "DemoCredentials",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemoCredentials_SubjectId",
                table: "DemoCredentials",
                column: "SubjectId",
                unique: true);
        }

        /// <summary>Removes the demo credential table when rolling back this migration.</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemoCredentials");
        }
    }
}
