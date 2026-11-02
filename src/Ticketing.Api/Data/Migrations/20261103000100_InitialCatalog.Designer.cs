using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ticketing.Api.Data.Migrations;

[DbContext(typeof(TicketingDbContext))]
[Migration("20261103000100_InitialCatalog")]
partial class InitialCatalog
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.11")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("Ticketing.Api.Catalog.Ticket", ticket =>
        {
            ticket.Property<int>("Id").HasColumnType("integer");
            ticket.Property<int>("Capacity").HasColumnType("integer");
            ticket.Property<string>("Name").IsRequired().HasMaxLength(40).HasColumnType("character varying(40)");
            ticket.Property<decimal>("PriceAed").HasPrecision(12, 2).HasColumnType("numeric(12,2)");
            ticket.Property<int>("RemainingQuota").HasColumnType("integer");
            ticket.HasKey("Id");
            ticket.HasIndex("Name").IsUnique();
            ticket.ToTable("Tickets", table =>
            {
                table.HasCheckConstraint("CK_Tickets_Quota", "\"RemainingQuota\" BETWEEN 0 AND \"Capacity\"");
                table.HasCheckConstraint("CK_Tickets_Price", "\"PriceAed\" > 0");
            });
            ticket.HasData(
                new { Id = 1, Name = "Gold", PriceAed = 100m, Capacity = 100, RemainingQuota = 100 },
                new { Id = 2, Name = "Premium", PriceAed = 200m, Capacity = 100, RemainingQuota = 100 },
                new { Id = 3, Name = "VIP", PriceAed = 500m, Capacity = 100, RemainingQuota = 100 });
        });
    }
}
