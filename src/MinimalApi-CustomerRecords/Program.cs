using Microsoft.EntityFrameworkCore;
using MinimalApi_CustomerRecords.Data;
using MinimalApi_CustomerRecords.Models;
using MiniValidation;
using NetDevPack.Identity;
using NetDevPack.Identity.Jwt;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<MinimalContextDb>(options =>
    options.UseSqlServer(builder.Configuration["DefaultConnection"]));

builder.Services.AddIdentityEntityFrameworkContextConfiguration(options =>
    options.UseSqlServer(builder.Configuration["DefaultConnection"],
    b => b.MigrationsAssembly("MinimalApi_CustomerRecords")));

builder.Services.AddIdentityConfiguration();
builder.Services.AddJwtConfiguration(builder.Configuration, "AppSettings");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthConfiguration();

app.UseHttpsRedirection();

app.MapGet("/customers", async (
    MinimalContextDb context) =>

    await context.Customers.ToListAsync())
    .WithName("GetCustomers")
    .WithTags("Customer");

app.MapGet("/customer/{id}", async (
    Guid id, MinimalContextDb context) =>

    await context.Customers.FindAsync(id)
        is Customer customer
            ? Results.Ok(customer)
            : Results.NotFound())
    .Produces<Customer>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound)
    .WithName("GetCustomerById")
    .WithTags("Customer");

app.MapPost("/customer", async (
    Customer customer, MinimalContextDb context) =>
    {
        if (!MiniValidator.TryValidate(customer, out var errors))
            return Results.ValidationProblem(errors);

        context.Customers.Add(customer);
        var result = await context.SaveChangesAsync();

        return result > 0
            //? Results.Created($"/customer/{customer.Id}", customer) // Outra forma seria retonar o Result.CreatedAtRoute
            ? Results.CreatedAtRoute("GetCustomerById", new { id = customer.Id }, customer)
            : Results.BadRequest("An error ocurred while saving the record");
    })
    .ProducesValidationProblem()
    .Produces<Customer>(StatusCodes.Status201Created)
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("PostCustomer")
    .WithTags("Customer");

app.MapPut("/customer/{id}", async (
    Guid id, MinimalContextDb context, Customer customer) =>
    {
        var customerFromDatabase = await context.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (customerFromDatabase == null) return Results.NotFound();

        if (!MiniValidator.TryValidate(customer, out var errors))
            return Results.ValidationProblem(errors);

        context.Customers.Update(customer);
        var result = await context.SaveChangesAsync();

        return result > 0
            ? Results.NoContent()
            : Results.BadRequest("An error ocurred while saving the record");
    })
    .ProducesValidationProblem()
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("PutCustomer")
    .WithTags("Customer");

app.MapDelete("/customer/{id}", async (
    Guid id, MinimalContextDb context) =>
    {
        var customerFromDatabase = await context.Customers.FindAsync(id);
        if (customerFromDatabase == null) return Results.NotFound();

        context.Customers.Remove(customerFromDatabase);
        var result = await context.SaveChangesAsync();

        return result > 0
            ? Results.NoContent()
            : Results.BadRequest("An error ocurred while saving the record");
    })
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("DeleteCustomer")
    .WithTags("Customer");

app.Run();
