using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplicationInAspire.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceMatchingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAvailable",
                table: "Resources",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "Resources",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "AssignedResourceId",
                table: "Needs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Needs_AssignedResourceId",
                table: "Needs",
                column: "AssignedResourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Needs_Resources_AssignedResourceId",
                table: "Needs",
                column: "AssignedResourceId",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Needs_Resources_AssignedResourceId",
                table: "Needs");

            migrationBuilder.DropIndex(
                name: "IX_Needs_AssignedResourceId",
                table: "Needs");

            migrationBuilder.DropColumn(
                name: "IsAvailable",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "AssignedResourceId",
                table: "Needs");
        }
    }
}
