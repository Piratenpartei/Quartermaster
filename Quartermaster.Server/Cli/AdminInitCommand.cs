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
        Console.Write("Admin username: ");
        var username = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(username)) {
            Console.Error.WriteLine("Username must not be empty.");
            return 1;
        }

        Console.Write("Admin password: ");
        var password = ReadPassword();
        if (string.IsNullOrEmpty(password) || password.Length < 12) {
            Console.Error.WriteLine("Password must be at least 12 characters long.");
            return 1;
        }

        Console.Write("Repeat password: ");
        var confirm = ReadPassword();
        if (password != confirm) {
            Console.Error.WriteLine("Passwords do not match.");
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
            Console.Error.WriteLine("DatabaseSettings:ConnectionString is not configured.");
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

        DbContext.SupplementDefaults(provider);

        var userRepo = scope.ServiceProvider.GetRequiredService<UserRepository>();
        var existing = userRepo.GetByUsername(username);
        if (existing != null) {
            Console.Error.WriteLine($"User '{username}' already exists.");
            return 1;
        }

        var settings = new RootAccountSettings { Username = username, Password = password };
        userRepo.SupplementDefaults(settings);

        Console.WriteLine($"Admin user '{username}' created.");
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
