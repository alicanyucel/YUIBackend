using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YUIBackend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialOperationLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "FinancialTransactions",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "FinancialTransactions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransactions_IdempotencyKey",
                table: "FinancialTransactions",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransactions_OccurredAt_Status",
                table: "FinancialTransactions",
                columns: new[] { "OccurredAt", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FinancialTransactions_IdempotencyKey",
                table: "FinancialTransactions");

            migrationBuilder.DropIndex(
                name: "IX_FinancialTransactions_OccurredAt_Status",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "FinancialTransactions");
        }
    }
}
