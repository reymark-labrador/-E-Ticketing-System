using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Checkout;
using Ticketing.Api.Data;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Ticketing")
    ?? throw new InvalidOperationException("Set ConnectionStrings:Ticketing before starting the API.");

builder.Services.AddDbContext<TicketingDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<PaymentConfirmationService>();
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddSingleton<IPaymentHandler, CreditCardHandler>();
builder.Services.AddSingleton<IPaymentHandler, QRHandler>();
builder.Services.AddHostedService<QrPaymentWorker>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

var app = builder.Build();
app.UseCors();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TicketingDbContext>();
    await db.Database.MigrateAsync();
}

app.MapGet("/api/tickets", (TicketingDbContext db, CancellationToken cancellationToken) =>
    db.Tickets.AsNoTracking().OrderBy(t => t.Id).ToListAsync(cancellationToken));

app.MapPost("/api/orders", async (CreateOrderRequest request, CheckoutService checkout, CancellationToken cancellationToken) =>
{
    var validation = new List<ValidationResult>();
    if (!Validator.TryValidateObject(request, new ValidationContext(request), validation, validateAllProperties: true))
    {
        return Results.ValidationProblem(validation
            .GroupBy(v => v.MemberNames.FirstOrDefault() ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.Select(v => v.ErrorMessage ?? "Invalid value.").ToArray()));
    }

    try
    {
        var order = await checkout.CreateAsync(request, cancellationToken);
        var location = $"/api/orders/{order.Id}";
        return order.Status == CheckoutConstants.StatusPending
            ? Results.Accepted(location, order)
            : Results.Created(location, order);
    }
    catch (CheckoutValidationException ex)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [string.Empty] = [ex.Message]
        });
    }
    catch (InsufficientQuotaException)
    {
        return Results.Conflict(new { error = "Insufficient ticket availability." });
    }
    catch (IdempotencyConflictException)
    {
        return Results.Conflict(new { error = "RequestId was already used with different inputs." });
    }
});

app.MapGet("/api/orders/{id:guid}", async (Guid id, CheckoutService checkout, CancellationToken cancellationToken) =>
{
    var order = await checkout.GetAsync(id, cancellationToken);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

app.Run();

public partial class Program;
