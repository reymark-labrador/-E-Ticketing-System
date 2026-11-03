using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Ticketing.Api.Catalog;
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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "E-Ticketing API",
        Version = "v1",
        Description = "Catalog and checkout for the e-ticketing demo. Card returns 201; QR returns 202 until confirmation."
    });
});

var app = builder.Build();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "E-Ticketing API v1");
});

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TicketingDbContext>();
    await db.Database.MigrateAsync();
}

app.MapGet("/api/tickets", (TicketingDbContext db, CancellationToken cancellationToken) =>
        db.Tickets.AsNoTracking().OrderBy(t => t.Id).ToListAsync(cancellationToken))
    .WithName("ListTickets")
    .WithTags("Catalog")
    .WithSummary("List ticket types")
    .WithDescription("Returns the seeded catalog with live remaining quota.")
    .Produces<List<Ticket>>(StatusCodes.Status200OK);

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
})
    .WithName("CreateOrder")
    .WithTags("Checkout")
    .WithSummary("Create a checkout order")
    .WithDescription(
        "Reserves quota and starts payment. Card succeeds immediately (201 Created). " +
        "QR returns 202 Accepted while pending. Conflicts return 409 when quota is insufficient " +
        "or the same requestId is reused with different inputs.")
    .Accepts<CreateOrderRequest>("application/json")
    .Produces<OrderResponse>(StatusCodes.Status201Created)
    .Produces<OrderResponse>(StatusCodes.Status202Accepted)
    .Produces(StatusCodes.Status409Conflict)
    .ProducesValidationProblem();

app.MapGet("/api/orders", async (CheckoutService checkout, CancellationToken cancellationToken) =>
{
    var orders = await checkout.ListAsync(cancellationToken);
    return Results.Ok(orders);
})
    .WithName("ListOrders")
    .WithTags("Checkout")
    .WithSummary("List recent orders")
    .WithDescription("Returns the latest 50 persisted orders as a read-only projection, newest first.")
    .Produces<List<OrderSummaryResponse>>(StatusCodes.Status200OK);

app.MapGet("/api/orders/{id:guid}", async (Guid id, CheckoutService checkout, CancellationToken cancellationToken) =>
{
    var order = await checkout.GetAsync(id, cancellationToken);
    return order is null ? Results.NotFound() : Results.Ok(order);
})
    .WithName("GetOrder")
    .WithTags("Checkout")
    .WithSummary("Get an order by id")
    .WithDescription("Returns the order, payment status, and ledger entries when payment has succeeded.")
    .Produces<OrderResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

app.Run();

public partial class Program;
