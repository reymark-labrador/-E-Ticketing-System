using Microsoft.EntityFrameworkCore.Migrations;

namespace Ticketing.Api.Data.Migrations;

public partial class InitialCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Tickets",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                PriceAed = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                Capacity = table.Column<int>(type: "integer", nullable: false),
                RemainingQuota = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Tickets", t => t.Id);
                table.CheckConstraint("CK_Tickets_Quota", "\"RemainingQuota\" BETWEEN 0 AND \"Capacity\"");
                table.CheckConstraint("CK_Tickets_Price", "\"PriceAed\" > 0");
            });

        migrationBuilder.CreateIndex("IX_Tickets_Name", "Tickets", "Name", unique: true);
        migrationBuilder.InsertData(
            table: "Tickets",
            columns: new[] { "Id", "Name", "PriceAed", "Capacity", "RemainingQuota" },
            values: new object[,]
            {
                { 1, "Gold", 100m, 100, 100 },
                { 2, "Premium", 200m, 100, 100 },
                { 3, "VIP", 500m, 100, 100 }
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("Tickets");
}
