using System;
using LinqToDB;
using LinqToDB.AspNet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FluentMigrator.Runner;
using Quartermaster.Data;
using Quartermaster.Data.Migrations;
using Quartermaster.Data.Users;

namespace Quartermaster.Server.Cli;

public static class AdminInitCommand {
    public static int Execute(string[] args) {
        Console.Write("Admin-Benutzername: ");
        var username = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(username)) {
            Console.Error.WriteLine("Benutzername darf nicht leer sein.");
            return 1;
        }

        Console.Write("Admin-Passwort: ");
        var password = ReadPassword();
        if (string.IsNullOrEmpty(password) || password.Length < 12) {
            Console.Error.WriteLine("Passwort muss mindestens 12 Zeichen lang sein.");
            return 1;
        }

        Console.Write("Passwort wiederholen: ");
        var confirm = ReadPassword();
        if (password != confirm) {
            Console.Error.WriteLine("Passwörter stimmen nicht überein.");
            return 1;
        }

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connStr = config.GetValue<string>("DatabaseSettings:ConnectionString");
        if (string.IsNullOrEmpty(connStr)) {
            Console.Error.WriteLine("DatabaseSettings:ConnectionString ist nicht konfiguriert.");
            return 1;
        }

        var services = new ServiceCollection();
        services.AddFluentMigratorCore()
            .ConfigureRunner(rb => {
                rb.AddMySql8()
                    .WithGlobalConnectionString(connStr)
                    .ScanIn(typeof(M001_InitialStructureMigration).Assembly).For.Migrations();
            });
        services.AddLinqToDBContext<DbContext>((provider, options)
            => options.UseMySqlConnector(connStr));
        DbContext.AddRepositories(services);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var migrator = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        migrator.MigrateUp();

        var userRepo = scope.ServiceProvider.GetRequiredService<UserRepository>();
        var existing = userRepo.GetByUsername(username);
        if (existing != null) {
            Console.Error.WriteLine($"Benutzer '{username}' existiert bereits.");
            return 1;
        }

        var settings = new RootAccountSettings { Username = username, Password = password };
        userRepo.SupplementDefaults(settings);

        Console.WriteLine($"Admin-Benutzer '{username}' wurde erstellt.");
        return 0;
    }

    private static string ReadPassword() {
        var password = "";
        while (true) {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) {
                Console.WriteLine();
                break;
            }
            if (key.Key == ConsoleKey.Backspace && password.Length > 0) {
                password = password[..^1];
                Console.Write("\b \b");
            }
            else if (!char.IsControl(key.KeyChar)) {
                password += key.KeyChar;
                Console.Write("*");
            }
        }
        return password;
    }
}
