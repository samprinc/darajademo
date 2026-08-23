using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DarajaDemo.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MpesaTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    MerchantRequestId = table.Column<string>(type: "text", nullable: true),
                    CheckoutRequestId = table.Column<string>(type: "text", nullable: true),
                    TransId = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    BillRefNumber = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResultCode = table.Column<int>(type: "integer", nullable: true),
                    ResultDesc = table.Column<string>(type: "text", nullable: true),
                    RawPayloadJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MpesaTransactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MpesaTransactions_CheckoutRequestId",
                table: "MpesaTransactions",
                column: "CheckoutRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MpesaTransactions_PhoneNumber",
                table: "MpesaTransactions",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_MpesaTransactions_TransId",
                table: "MpesaTransactions",
                column: "TransId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MpesaTransactions");
        }
    }
}
