using FastEndpoints;
using FluentMigrator.Runner;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api.Users;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;

namespace Quartermaster.Server;

public static class Program {
    public static void Main(string[] args) {
        //AdministrativeDivisionLoader.Load("DE_Base.txt", "DE_PostCodes.txt");

        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddAuthorization();

        builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("DatabaseSettings"));
        builder.Services.Configure<RootAccountSettings>(builder.Configuration.GetSection("RootAccountSettings"));
        builder.Services.Configure<SamlSettings>(builder.Configuration.GetSection("SamlSettings"));

        builder.Services.AddFluentMigratorCore()
            .ConfigureRunner(rb => {
                rb.AddMySql8()
                    .WithGlobalConnectionString(builder.Configuration.GetValue<string>("DatabaseSettings:ConnectionString"))
                    .ScanIn(typeof(DbContext).Assembly).For.Migrations();
            });

        builder.Services.AddSingleton<DbContext>();

        builder.Services.AddCors(opt => {
            opt.AddPolicy("Default", policy
                => policy.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod());
        });

        builder.Services.AddValidatorsFromAssembly(typeof(LoginRequest).Assembly,
            filter: x => x.ValidatorType.BaseType?.GetGenericTypeDefinition() != typeof(Validator<>));
        builder.Services.AddFastEndpoints();

        var app = builder.Build();

        using var scope = app.Services.CreateScope();
        var migrator = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        migrator.MigrateUp();

        app.UseCors("Default");
        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseAuthorization();
        app.UseFastEndpoints();

        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();

#pragma warning disable ASP0014 // MapFallbackToFile does not exist as direct mapping.
        app.UseEndpoints(ep => ep.MapFallbackToFile("index.html"));
#pragma warning restore ASP0014

        app.Run();
    }
}