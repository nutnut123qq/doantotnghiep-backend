using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockInvestment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationChannelConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationChannelConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlackWebhookUrl = table.Column<string>(type: "text", nullable: true),
                    EnabledSlack = table.Column<bool>(type: "boolean", nullable: false),
                    TelegramChatId = table.Column<string>(type: "text", nullable: true),
                    EnabledTelegram = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationChannelConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationChannelConfigs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationChannelConfigs_UserId",
                table: "NotificationChannelConfigs",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationChannelConfigs");
        }
    }
}
