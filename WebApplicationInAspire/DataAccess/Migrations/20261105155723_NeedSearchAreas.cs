using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplicationInAspire.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class NeedSearchAreas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AreaEast",
                table: "Needs",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AreaNorth",
                table: "Needs",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AreaSouth",
                table: "Needs",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AreaWest",
                table: "Needs",
                type: "double",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resources_Latitude_Longitude",
                table: "Resources",
                columns: new[] { "Latitude", "Longitude" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Resources_Latitude_Longitude",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "AreaEast",
                table: "Needs");

            migrationBuilder.DropColumn(
                name: "AreaNorth",
                table: "Needs");

            migrationBuilder.DropColumn(
                name: "AreaSouth",
                table: "Needs");

            migrationBuilder.DropColumn(
                name: "AreaWest",
                table: "Needs");
        }
    }
}
