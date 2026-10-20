using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduCore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RoleLinkedDashboards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClassId",
                table: "Students",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubjectId",
                table: "Students",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SchoolClasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    GradeLevel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolClasses", x => new { x.SchoolId, x.Id });
                    table.ForeignKey(
                        name: "FK_SchoolClasses_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Teachers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teachers", x => new { x.SchoolId, x.Id });
                    table.ForeignKey(
                        name: "FK_Teachers_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeachingAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeachingAssignments", x => new { x.SchoolId, x.Id });
                    table.ForeignKey(
                        name: "FK_TeachingAssignments_SchoolClasses_SchoolId_ClassId",
                        columns: x => new { x.SchoolId, x.ClassId },
                        principalTable: "SchoolClasses",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeachingAssignments_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeachingAssignments_Teachers_SchoolId_TeacherId",
                        columns: x => new { x.SchoolId, x.TeacherId },
                        principalTable: "Teachers",
                        principalColumns: new[] { "SchoolId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Students_SchoolId_ClassId",
                table: "Students",
                columns: new[] { "SchoolId", "ClassId" });

            migrationBuilder.CreateIndex(
                name: "IX_Students_SchoolId_SubjectId",
                table: "Students",
                columns: new[] { "SchoolId", "SubjectId" },
                unique: true,
                filter: "[SubjectId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolClasses_SchoolId_Name",
                table: "SchoolClasses",
                columns: new[] { "SchoolId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_SchoolId_SubjectId",
                table: "Teachers",
                columns: new[] { "SchoolId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignments_SchoolId_ClassId",
                table: "TeachingAssignments",
                columns: new[] { "SchoolId", "ClassId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignments_SchoolId_TeacherId_ClassId_SubjectName",
                table: "TeachingAssignments",
                columns: new[] { "SchoolId", "TeacherId", "ClassId", "SubjectName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_SchoolClasses_SchoolId_ClassId",
                table: "Students",
                columns: new[] { "SchoolId", "ClassId" },
                principalTable: "SchoolClasses",
                principalColumns: new[] { "SchoolId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_SchoolClasses_SchoolId_ClassId",
                table: "Students");

            migrationBuilder.DropTable(
                name: "TeachingAssignments");

            migrationBuilder.DropTable(
                name: "SchoolClasses");

            migrationBuilder.DropTable(
                name: "Teachers");

            migrationBuilder.DropIndex(
                name: "IX_Students_SchoolId_ClassId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_SchoolId_SubjectId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ClassId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "Students");
        }
    }
}
