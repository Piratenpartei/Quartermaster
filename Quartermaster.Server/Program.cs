using FastEndpoints;
using FluentMigrator.Runner;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api.Users;
using Quartermaster.Data;
using LinqToDB.AspNet;
using LinqToDB;
using Quartermaster.Data.Migrations;
using Quartermaster.Server.Members;

namespace Quartermaster.Server;

public static class Program {
    public static void Main(string[] args) {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddAuthorization();

        builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("DatabaseSettings"));
        builder.Services.Configure<RootAccountSettings>(builder.Configuration.GetSection("RootAccountSettings"));
        builder.Services.Configure<SamlSettings>(builder.Configuration.GetSection("SamlSettings"));

        builder.Services.AddFluentMigratorCore()
            .ConfigureRunner(rb => {
                var connStr = builder.Configuration.GetValue<string>("DatabaseSettings:ConnectionString");

                rb.AddMySql8().WithGlobalConnectionString(builder.Configuration.GetValue<string>("DatabaseSettings:ConnectionString"))
                    .ScanIn(typeof(M001_InitialStructureMigration).Assembly).For.Migrations();
            });

        builder.Services.AddLinqToDBContext<DbContext>((provider, options)
            => options.UseMySqlConnector(builder.Configuration.GetValue<string>("DatabaseSettings:ConnectionString")!));
        DbContext.AddRepositories(builder.Services);

        builder.Services.AddSingleton<MemberImportService>();
        builder.Services.AddHostedService<MemberImportHostedService>();

        builder.Services.AddSingleton<Quartermaster.Server.Events.MemberEmailService>();
        builder.Services.AddScoped<Quartermaster.Server.Events.ChecklistItemExecutor>();

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

// Down migration disabled to preserve data between restarts
// #if DEBUG
//         if (migrator.HasMigrationsToApplyDown(0))
//             migrator.MigrateDown(0);
// #endif

        migrator.MigrateUp();

        DbContext.SupplementDefaults(app.Services);

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