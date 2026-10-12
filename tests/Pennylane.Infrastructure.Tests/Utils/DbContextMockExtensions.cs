// <copyright file="DbContextMockExtensions.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pulse.ContactRegistry.Infrastructure.Context;

namespace Pennylane.Infrastructure.Tests.Utils
{
    public static class DbContextMockExtensions
    {

        public static PennylaneContext CreateInMemoryPennylaneContext()
        {
            var serviceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            var options = new DbContextOptionsBuilder<PennylaneContext>()
                .UseInMemoryDatabase("InMemoryAppDb")
                .UseInternalServiceProvider(serviceProvider)
                .Options;

            var dbContext = new PennylaneContext(options);
            dbContext.Database.EnsureDeleted();
            dbContext.Database.EnsureCreated();
            return dbContext;
        }

        public static DbContextOptions<PennylaneContext> CreateSqliteInMemoryOptions(string databaseName)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return new DbContextOptionsBuilder<PennylaneContext>()
                .UseSqlite(connection)
                .Options;
        }

        public static TestContext CreateSqlLitePennylaneContext(DbContextOptions<PennylaneContext> options)
        {
            var serviceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();


            var context = new TestContext(options);
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
            return context;
        }
    }
}
