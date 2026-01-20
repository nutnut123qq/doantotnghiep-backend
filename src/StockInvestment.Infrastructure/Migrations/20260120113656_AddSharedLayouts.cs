using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockInvestment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedLayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SharedLayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LayoutJson = table.Column<string>(type: "text", nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedLayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharedLayouts_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SharedLayouts_Code",
                table: "SharedLayouts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedLayouts_ExpiresAt",
                table: "SharedLayouts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_SharedLayouts_OwnerId",
                table: "SharedLayouts",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SharedLayouts");
        }
    }
}
