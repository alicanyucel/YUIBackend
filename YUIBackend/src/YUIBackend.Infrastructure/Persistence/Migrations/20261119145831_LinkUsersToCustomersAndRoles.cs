using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YUIBackend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkUsersToCustomersAndRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "InitialUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "InitialUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE [InitialUsers] SET [IsAdmin] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_InitialUsers_CustomerId",
                table: "InitialUsers",
                column: "CustomerId",
                unique: true,
                filter: "[CustomerId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_InitialUsers_Customers_CustomerId",
                table: "InitialUsers",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InitialUsers_Customers_CustomerId",
                table: "InitialUsers");

            migrationBuilder.DropIndex(
                name: "IX_InitialUsers_CustomerId",
                table: "InitialUsers");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "InitialUsers");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "InitialUsers");
        }
    }
}
