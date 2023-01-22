using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MinimalApi_CustomerRecords.Data;
using MinimalApi_CustomerRecords.Models;
using MiniValidation;
using NetDevPack.Identity.Jwt;
using NetDevPack.Identity.Model;

var builder = WebApplication.CreateBuilder(args);

#region Configure services

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Minimal API Sample",
        Description = "",
        License = new OpenApiLicense { Name = "MIT", Url = new Uri("https://opensource.org/licenses/MIT") }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Insira o token JWT desta maneira: Bearer {seu token}",
        Name = "Authorization",
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

builder.Services.AddDbContext<MinimalContextDb>(options =>
    options.UseSqlServer(builder.Configuration["DefaultConnection"]));

builder.Services.AddIdentityEntityFrameworkContextConfiguration(options =>
    options.UseSqlServer(builder.Configuration["DefaultConnection"],
    b => b.MigrationsAssembly("MinimalApi-CustomerRecords")));

builder.Services.AddIdentityConfiguration();
builder.Services.AddJwtConfiguration(builder.Configuration, "AppSettings");

builder.Services.AddAuthorization(options => options.AddPolicy("DeleteCustomer", policy => policy.RequireClaim("DeleteCustomer")));

var app = builder.Build();

#endregion

#region Configure pipeline

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthConfiguration();
app.UseHttpsRedirection();

MapActions(app);

app.Run();

#endregion

#region Endpoints

void MapActions(WebApplication app)
{

    app.MapPost("/register", [AllowAnonymous] async (
        UserManager<IdentityUser> userManager,
        IOptions<AppJwtSettings> appJwtSettings,
        RegisterUser registerUser) =>
        {
            if (registerUser == null)
                return Results.BadRequest("User is required");

            if (!MiniValidator.TryValidate(registerUser, out var errors))
                return Results.ValidationProblem(errors);

            var user = new IdentityUser
            {
                UserName = registerUser.Email,
                Email = registerUser.Email,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, registerUser.Password);

            if (!result.Succeeded)
                return Results.BadRequest(result.Errors);

            var jwt = new JwtBuilder()
                .WithUserManager(userManager)
                .WithJwtSettings(appJwtSettings.Value)
                .WithEmail(user.Email)
                .WithJwtClaims()
                .WithUserClaims()
                .WithUserRoles()
                .BuildUserResponse();

            return Results.Ok(jwt);
        })
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .WithName("UserRegister")
        .WithTags("User");

    app.MapPost("/login", [AllowAnonymous] async (
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        IOptions<AppJwtSettings> appJwtSettings,
        LoginUser loginUser) =>
        {
            if (loginUser == null)
                return Results.BadRequest("User is required");

            if (!MiniValidator.TryValidate(loginUser, out var errors))
                return Results.ValidationProblem(errors);

            var result = await signInManager.PasswordSignInAsync(loginUser.Email, loginUser.Password, false, true);

            if (result.IsLockedOut)
                return Results.BadRequest("Blocked user");

            if (!result.Succeeded)
                return Results.BadRequest("Invalid user or password");

            var jwt = new JwtBuilder()
                .WithUserManager(userManager)
                .WithJwtSettings(appJwtSettings.Value)
                .WithEmail(loginUser.Email)
                .WithJwtClaims()
                .WithUserClaims()
                .WithUserRoles()
                .BuildUserResponse();

            return Results.Ok(jwt);
        })
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .WithName("UserLogin")
        .WithTags("User");

    app.MapGet("/customers", [AllowAnonymous] async (
        MinimalContextDb context) =>

        await context.Customers.ToListAsync())
        .WithName("GetCustomers")
        .WithTags("Customer");

    app.MapGet("/customer/{id}", [AllowAnonymous] async (
        Guid id, MinimalContextDb context) =>

        await context.Customers.FindAsync(id)
            is Customer customer
                ? Results.Ok(customer)
                : Results.NotFound())
        .Produces<Customer>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithName("GetCustomerById")
        .WithTags("Customer");

    app.MapPost("/customer", [Authorize] async (
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

    app.MapPut("/customer/{id}", [Authorize] async (
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

    app.MapDelete("/customer/{id}", [Authorize] async (
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
        .RequireAuthorization("DeleteCustomer")
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .WithName("DeleteCustomer")
        .WithTags("Customer");
}

#endregion