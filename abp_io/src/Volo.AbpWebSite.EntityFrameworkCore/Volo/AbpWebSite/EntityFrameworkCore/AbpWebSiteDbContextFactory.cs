using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace Volo.AbpWebSite.EntityFrameworkCore
{
    public class AbpWebSiteDbContextFactory : IDesignTimeDbContextFactory<AbpWebSiteDbContext>
    {
        public AbpWebSiteDbContext CreateDbContext(string[] args)
        {
            var configuration = BuildConfiguration();

            var builder = new DbContextOptionsBuilder<AbpWebSiteDbContext>().UseMySql(
                configuration.GetConnectionString("Default"),
                options => { options.ServerVersion(new Version(8, 0, 12), ServerType.MySql); });

            return new AbpWebSiteDbContext(builder.Options);
        }

        private static IConfigurationRoot BuildConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../Volo.AbpWebSite.Web/"))
                .AddJsonFile("appsettings.json", false);

            return builder.Build();
        }
    }
}