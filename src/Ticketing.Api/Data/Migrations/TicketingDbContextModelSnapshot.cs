using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Ticketing.Api.Data;

#nullable disable

namespace Ticketing.Api.Data.Migrations;

[DbContext(typeof(TicketingDbContext))]
partial class TicketingDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.11")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("Ticketing.Api.Catalog.Ticket", b =>
            {
                b.Property<int>("Id")
                    .HasColumnType("integer");

                b.Property<int>("Capacity")
                    .HasColumnType("integer");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(40)
                    .HasColumnType("character varying(40)");

                b.Property<decimal>("PriceAed")
                    .HasPrecision(12, 2)
                    .HasColumnType("numeric(12,2)");

                b.Property<int>("RemainingQuota")
                    .HasColumnType("integer");

                b.HasKey("Id");

                b.HasIndex("Name")
                    .IsUnique();

                b.ToTable("Tickets", null, t =>
                    {
                        t.HasCheckConstraint("CK_Tickets_Price", "\"PriceAed\" > 0");
                        t.HasCheckConstraint("CK_Tickets_Quota", "\"RemainingQuota\" BETWEEN 0 AND \"Capacity\"");
                    });

                b.HasData(
                    new
                    {
                        Id = 1,
                        Capacity = 100,
                        Name = "Gold",
                        PriceAed = 100m,
                        RemainingQuota = 100
                    },
                    new
                    {
                        Id = 2,
                        Capacity = 100,
                        Name = "Premium",
                        PriceAed = 200m,
                        RemainingQuota = 100
                    },
                    new
                    {
                        Id = 3,
                        Capacity = 100,
                        Name = "VIP",
                        PriceAed = 500m,
                        RemainingQuota = 100
                    });
            });

        modelBuilder.Entity("Ticketing.Api.Checkout.LedgerEntry", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<string>("Account")
                    .IsRequired()
                    .HasMaxLength(80)
                    .HasColumnType("character varying(80)");

                b.Property<decimal>("AmountAed")
                    .HasPrecision(12, 2)
                    .HasColumnType("numeric(12,2)");

                b.Property<string>("Side")
                    .IsRequired()
                    .HasMaxLength(10)
                    .HasColumnType("character varying(10)");

                b.Property<Guid>("TransactionId")
                    .HasColumnType("uuid");

                b.HasKey("Id");

                b.HasIndex("TransactionId", "Side")
                    .IsUnique();

                b.ToTable("LedgerEntries", null, t =>
                    {
                        t.HasCheckConstraint("CK_LedgerEntries_Amount", "\"AmountAed\" > 0");
                        t.HasCheckConstraint("CK_LedgerEntries_Side", "\"Side\" IN ('Debit', 'Credit')");
                    });
            });

        modelBuilder.Entity("Ticketing.Api.Checkout.Order", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<DateTimeOffset>("CreatedAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<string>("PaymentMethod")
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasColumnType("character varying(20)");

                b.Property<int>("Quantity")
                    .HasColumnType("integer");

                b.Property<string>("RequestId")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("character varying(100)");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasColumnType("character varying(20)");

                b.Property<int>("TicketId")
                    .HasColumnType("integer");

                b.Property<decimal>("TotalAed")
                    .HasPrecision(12, 2)
                    .HasColumnType("numeric(12,2)");

                b.Property<decimal>("UnitPriceAed")
                    .HasPrecision(12, 2)
                    .HasColumnType("numeric(12,2)");

                b.HasKey("Id");

                b.HasIndex("RequestId")
                    .IsUnique();

                b.HasIndex("TicketId");

                b.ToTable("Orders", null, t =>
                    {
                        t.HasCheckConstraint("CK_Orders_Quantity", "\"Quantity\" > 0");
                        t.HasCheckConstraint("CK_Orders_Total", "\"TotalAed\" > 0");
                        t.HasCheckConstraint("CK_Orders_UnitPrice", "\"UnitPriceAed\" > 0");
                    });
            });

        modelBuilder.Entity("Ticketing.Api.Checkout.PaymentTransaction", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<decimal>("AmountAed")
                    .HasPrecision(12, 2)
                    .HasColumnType("numeric(12,2)");

                b.Property<DateTimeOffset?>("ConfirmedAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<DateTimeOffset>("CreatedAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<DateTimeOffset?>("DueAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<Guid>("OrderId")
                    .HasColumnType("uuid");

                b.Property<string>("PaymentMethod")
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasColumnType("character varying(20)");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasColumnType("character varying(20)");

                b.HasKey("Id");

                b.HasIndex("OrderId")
                    .IsUnique();

                b.ToTable("Transactions", null, t =>
                    {
                        t.HasCheckConstraint("CK_Transactions_Amount", "\"AmountAed\" > 0");
                    });
            });

        modelBuilder.Entity("Ticketing.Api.Checkout.LedgerEntry", b =>
            {
                b.HasOne("Ticketing.Api.Checkout.PaymentTransaction", "Transaction")
                    .WithMany("LedgerEntries")
                    .HasForeignKey("TransactionId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();

                b.Navigation("Transaction");
            });

        modelBuilder.Entity("Ticketing.Api.Checkout.Order", b =>
            {
                b.HasOne("Ticketing.Api.Catalog.Ticket", null)
                    .WithMany()
                    .HasForeignKey("TicketId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
            });

        modelBuilder.Entity("Ticketing.Api.Checkout.PaymentTransaction", b =>
            {
                b.HasOne("Ticketing.Api.Checkout.Order", "Order")
                    .WithOne("Transaction")
                    .HasForeignKey("Ticketing.Api.Checkout.PaymentTransaction", "OrderId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();

                b.Navigation("Order");
            });

        modelBuilder.Entity("Ticketing.Api.Checkout.Order", b =>
            {
                b.Navigation("Transaction");
            });

        modelBuilder.Entity("Ticketing.Api.Checkout.PaymentTransaction", b =>
            {
                b.Navigation("LedgerEntries");
            });
#pragma warning restore 612, 618
    }
}
