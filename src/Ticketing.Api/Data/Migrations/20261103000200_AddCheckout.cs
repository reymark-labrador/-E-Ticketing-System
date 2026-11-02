using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ticketing.Api.Data.Migrations;

public partial class AddCheckout : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Orders",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RequestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                TicketId = table.Column<int>(type: "integer", nullable: false),
                Quantity = table.Column<int>(type: "integer", nullable: false),
                UnitPriceAed = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                TotalAed = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                PaymentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Orders", x => x.Id);
                table.CheckConstraint("CK_Orders_Quantity", "\"Quantity\" > 0");
                table.CheckConstraint("CK_Orders_UnitPrice", "\"UnitPriceAed\" > 0");
                table.CheckConstraint("CK_Orders_Total", "\"TotalAed\" > 0");
                table.ForeignKey(
                    name: "FK_Orders_Tickets_TicketId",
                    column: x => x.TicketId,
                    principalTable: "Tickets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Transactions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                PaymentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                AmountAed = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                DueAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Transactions", x => x.Id);
                table.CheckConstraint("CK_Transactions_Amount", "\"AmountAed\" > 0");
                table.ForeignKey(
                    name: "FK_Transactions_Orders_OrderId",
                    column: x => x.OrderId,
                    principalTable: "Orders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "LedgerEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                Account = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Side = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                AmountAed = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LedgerEntries", x => x.Id);
                table.CheckConstraint("CK_LedgerEntries_Amount", "\"AmountAed\" > 0");
                table.CheckConstraint("CK_LedgerEntries_Side", "\"Side\" IN ('Debit', 'Credit')");
                table.ForeignKey(
                    name: "FK_LedgerEntries_Transactions_TransactionId",
                    column: x => x.TransactionId,
                    principalTable: "Transactions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Orders_RequestId",
            table: "Orders",
            column: "RequestId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Orders_TicketId",
            table: "Orders",
            column: "TicketId");

        migrationBuilder.CreateIndex(
            name: "IX_Transactions_OrderId",
            table: "Transactions",
            column: "OrderId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_LedgerEntries_TransactionId_Side",
            table: "LedgerEntries",
            columns: new[] { "TransactionId", "Side" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "LedgerEntries");
        migrationBuilder.DropTable(name: "Transactions");
        migrationBuilder.DropTable(name: "Orders");
    }
}
