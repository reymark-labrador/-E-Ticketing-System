using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Catalog;
using Ticketing.Api.Checkout;

namespace Ticketing.Api.Data;

public sealed class TicketingDbContext(DbContextOptions<TicketingDbContext> options) : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<PaymentTransaction> Transactions => Set<PaymentTransaction>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var ticket = modelBuilder.Entity<Ticket>();
        ticket.ToTable("Tickets", table =>
        {
            table.HasCheckConstraint("CK_Tickets_Quota", "\"RemainingQuota\" BETWEEN 0 AND \"Capacity\"");
            table.HasCheckConstraint("CK_Tickets_Price", "\"PriceAed\" > 0");
        });
        ticket.Property(t => t.Id).ValueGeneratedNever();
        ticket.Property(t => t.Name).HasMaxLength(40);
        ticket.Property(t => t.PriceAed).HasPrecision(12, 2);
        ticket.HasIndex(t => t.Name).IsUnique();
        ticket.HasData(
            new Ticket { Id = 1, Name = "Gold", PriceAed = 100m, Capacity = 100, RemainingQuota = 100 },
            new Ticket { Id = 2, Name = "Premium", PriceAed = 200m, Capacity = 100, RemainingQuota = 100 },
            new Ticket { Id = 3, Name = "VIP", PriceAed = 500m, Capacity = 100, RemainingQuota = 100 });

        var order = modelBuilder.Entity<Order>();
        order.ToTable("Orders", table =>
        {
            table.HasCheckConstraint("CK_Orders_Quantity", "\"Quantity\" > 0");
            table.HasCheckConstraint("CK_Orders_UnitPrice", "\"UnitPriceAed\" > 0");
            table.HasCheckConstraint("CK_Orders_Total", "\"TotalAed\" > 0");
        });
        order.Property(o => o.RequestId).HasMaxLength(100);
        order.Property(o => o.PaymentMethod).HasMaxLength(20);
        order.Property(o => o.Status).HasMaxLength(20);
        order.Property(o => o.UnitPriceAed).HasPrecision(12, 2);
        order.Property(o => o.TotalAed).HasPrecision(12, 2);
        order.HasIndex(o => o.RequestId).IsUnique();
        order.HasOne(o => o.Transaction)
            .WithOne(t => t.Order)
            .HasForeignKey<PaymentTransaction>(t => t.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
        order.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(o => o.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        var payment = modelBuilder.Entity<PaymentTransaction>();
        payment.ToTable("Transactions", table =>
        {
            table.HasCheckConstraint("CK_Transactions_Amount", "\"AmountAed\" > 0");
        });
        payment.Property(t => t.PaymentMethod).HasMaxLength(20);
        payment.Property(t => t.Status).HasMaxLength(20);
        payment.Property(t => t.AmountAed).HasPrecision(12, 2);
        payment.HasIndex(t => t.OrderId).IsUnique();

        var ledger = modelBuilder.Entity<LedgerEntry>();
        ledger.ToTable("LedgerEntries", table =>
        {
            table.HasCheckConstraint("CK_LedgerEntries_Amount", "\"AmountAed\" > 0");
            table.HasCheckConstraint("CK_LedgerEntries_Side", "\"Side\" IN ('Debit', 'Credit')");
        });
        ledger.Property(e => e.Account).HasMaxLength(80);
        ledger.Property(e => e.Side).HasMaxLength(10);
        ledger.Property(e => e.AmountAed).HasPrecision(12, 2);
        ledger.HasIndex(e => new { e.TransactionId, e.Side }).IsUnique();
        ledger.HasOne(e => e.Transaction)
            .WithMany(t => t.LedgerEntries)
            .HasForeignKey(e => e.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
