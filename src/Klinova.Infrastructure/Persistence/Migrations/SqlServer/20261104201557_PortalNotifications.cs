using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Klinova.Infrastructure.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class PortalNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAtUtc",
                table: "Notifications",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReadAtUtc",
                table: "Notifications");
        }
    }
}
