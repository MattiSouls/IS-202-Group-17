using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplicationInAspire.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class WorkspaceNotificationsAndReliability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreationToken",
                table: "Resources",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "Resources",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "CreationToken",
                table: "Needs",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Needs",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Needs",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "Needs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Title = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Message = table.Column<string>(type: "varchar(1200)", maxLength: 1200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResourceId = table.Column<int>(type: "int", nullable: true),
                    NeedId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notifications_Needs_NeedId",
                        column: x => x.NeedId,
                        principalTable: "Needs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Notifications_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_OwnerId_CreationToken",
                table: "Resources",
                columns: new[] { "OwnerId", "CreationToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resources_Region_Type",
                table: "Resources",
                columns: new[] { "Region", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Needs_CreatedByUserId_CreationToken",
                table: "Needs",
                columns: new[] { "CreatedByUserId", "CreationToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Needs_Status_NeededAt",
                table: "Needs",
                columns: new[] { "Status", "NeededAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_NeedId",
                table: "Notifications",
                column: "NeedId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ResourceId",
                table: "Notifications",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_ReadAtUtc_Id",
                table: "Notifications",
                columns: new[] { "UserId", "ReadAtUtc", "Id" });
            migrationBuilder.DropIndex(
                name: "IX_Resources_OwnerId",
                table: "Resources");

            migrationBuilder.DropIndex(
                name: "IX_Needs_CreatedByUserId",
                table: "Needs");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Resources_OwnerId",
                table: "Resources",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Needs_CreatedByUserId",
                table: "Needs",
                column: "CreatedByUserId");
            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Resources_OwnerId_CreationToken",
                table: "Resources");

            migrationBuilder.DropIndex(
                name: "IX_Resources_Region_Type",
                table: "Resources");

            migrationBuilder.DropIndex(
                name: "IX_Needs_CreatedByUserId_CreationToken",
                table: "Needs");

            migrationBuilder.DropIndex(
                name: "IX_Needs_Status_NeededAt",
                table: "Needs");

            migrationBuilder.DropColumn(
                name: "CreationToken",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "CreationToken",
                table: "Needs");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Needs");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Needs");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Needs");

        }
    }
}
