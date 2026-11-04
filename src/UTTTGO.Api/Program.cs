using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UTTTGO.Api.Modules.Admin.Application;
using UTTTGO.Api.Modules.Admin.Contracts;
using UTTTGO.Api.Modules.Admin.Infrastructure;
using UTTTGO.Api.Modules.Admin.Validators;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Identity.Contracts;
using UTTTGO.Api.Modules.Identity.Infrastructure;
using UTTTGO.Api.Modules.Identity.Validators;
using UTTTGO.Api.Modules.Sellers.Application;
using UTTTGO.Api.Modules.Sellers.Contracts;
using UTTTGO.Api.Modules.Sellers.Infrastructure;
using UTTTGO.Api.Modules.Sellers.Validators;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(
        ApiError.Create(context.HttpContext, 400, "INVALID_REQUEST", "Solicitud inválida."));
});
builder.Services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IOwnProfileService, OwnProfileService>();
builder.Services.AddScoped<IUpdateOwnProfileService, UpdateOwnProfileService>();
builder.Services.AddScoped<IValidator<UpdateOwnProfileRequest>, UpdateOwnProfileRequestValidator>();
builder.Services.AddSingleton<IProfileUpdateRepository, UnconfiguredProfileUpdateRepository>();
builder.Services.AddSingleton<IApprovedAvatarReadAccess, UnconfiguredApprovedAvatarReadAccess>();
// No external credentials are configured yet. Real adapters must replace these fail-closed registrations.
builder.Services.AddSingleton<IAuthRateLimitGate, UnconfiguredAuthRateLimitGate>();
builder.Services.AddSingleton<ISupabaseRegistration, UnconfiguredSupabaseRegistration>();
builder.Services.AddSingleton<IUserRegistrationRepository, UnconfiguredUserRegistrationRepository>();
builder.Services.AddSingleton<ISupabaseLogin, UnconfiguredSupabaseLogin>();
builder.Services.AddSingleton<ILoginProfileRepository, UnconfiguredLoginProfileRepository>();
builder.Services.AddSingleton<ILoginJwtGate, UnconfiguredLoginJwtGate>();
builder.Services.AddSingleton<IValidatedJwtSubjectReader, UnconfiguredValidatedJwtSubjectReader>();
builder.Services.AddSingleton<ISupabaseSessionIdentity, UnconfiguredSupabaseSessionIdentity>();
builder.Services.AddScoped<IAdminUsersService, AdminUsersService>();
builder.Services.AddScoped<IValidator<AdminUsersQueryRequest>, AdminUsersQueryRequestValidator>();
builder.Services.AddSingleton<IAdminUserListReader, UnconfiguredAdminUserListReader>();
builder.Services.AddSingleton<IAdminUsersRateLimitGate, UnconfiguredAdminUsersRateLimitGate>();
builder.Services.AddSingleton<IAdminUsersAudit, UnconfiguredAdminUsersAudit>();
builder.Services.AddScoped<IValidator<SellerIdRequest>, SellerIdRequestValidator>();
builder.Services.AddScoped<ISellerProfileService, SellerProfileService>();
builder.Services.AddSingleton<IPublicSellerRateLimitGate, UnconfiguredPublicSellerRateLimitGate>();
builder.Services.AddSingleton<ISellerProfileRepository, UnconfiguredSellerProfileRepository>();
builder.Services.AddSingleton<ISellerRatingsReader, UnconfiguredSellerRatingsReader>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
