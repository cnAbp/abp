﻿using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Geetest.Core;
using Geetest.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.WebEncoders;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Volo.Abp;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.Mvc.UI;
using Volo.Abp.AspNetCore.Mvc.UI.Bootstrap;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theming;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Autofac;
using Volo.Abp.Configuration;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.Web;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.Settings;
using Volo.Abp.PermissionManagement;
using Volo.Abp.PermissionManagement.Identity;
using Volo.Abp.Threading;
using Volo.Abp.UI;
using Volo.Abp.VirtualFileSystem;
using Volo.AbpWebSite.Bundling;
using Volo.AbpWebSite.EntityFrameworkCore;
using Volo.Blogging;
using Volo.Blogging.Files;
using Volo.Docs;
using Volo.Abp.SettingManagement;

namespace Volo.AbpWebSite
{
    [DependsOn(
        typeof(AbpWebSiteApplicationModule),
        typeof(AbpWebSiteEntityFrameworkCoreModule),
        typeof(AbpAutofacModule),
        typeof(AbpAspNetCoreMvcUiThemeSharedModule),
        typeof(DocsApplicationModule),
        typeof(DocsWebModule),
        typeof(AbpAccountWebModule),
        typeof(AbpIdentityApplicationModule),
        typeof(AbpIdentityWebModule),
        typeof(AbpPermissionManagementApplicationModule),
        typeof(AbpPermissionManagementDomainIdentityModule),
        typeof(BloggingApplicationModule),
        typeof(BloggingWebModule)
        )]
    public class AbpWebSiteWebModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var hostingEnvironment = context.Services.GetHostingEnvironment();
            var configuration = context.Services.GetConfiguration();

            ConfigureLanguages();
            ConfigureDatabaseServices(configuration);
            ConfigureVirtualFileSystem(hostingEnvironment);
            ConfigureBundles();
            ConfigureTheme();
            ConfigureBlogging(hostingEnvironment);

            context.Services.AddTransient<IGeetestManager, GeetestManager>();
            context.Services.AddTransient<IClientInfoProvider, ClientInfoProvider>();
            context.Services.AddSingleton<IGeetestConfiguration, GeetestConfiguration>();

            context.Services.AddSingleton<IGeetestConfiguration>(provider =>
                new GeetestConfiguration(provider.GetRequiredService<IClientInfoProvider>())
                {
                    Id = configuration["Captcha:Geetest:Id"],
                    Key = configuration["Captcha:Geetest:Key"]
                });

            ConfigureBlogging(hostingEnvironment);
        }

        private void ConfigureBlogging(IHostingEnvironment hostingEnvironment)
        {
            Configure<BlogFileOptions>(options =>
            {
                options.FileUploadLocalFolder = Path.Combine(hostingEnvironment.WebRootPath, "files");
            });
        }

        private void ConfigureLanguages()
        {
            Configure<AbpLocalizationOptions>(options =>
            {
                options.Languages.Add(new LanguageInfo("zh-Hans", "zh-Hans", "简体中文"));
            });
        }

        private void ConfigureBundles()
        {
            Configure<BundlingOptions>(options =>
            {
                options
                    .StyleBundles
                    .Add(AbpIoBundles.Styles.Global, bundle =>
                    {
                        bundle.
                            AddBaseBundles(StandardBundles.Styles.Global)
                            .AddFiles(
                                "/scss/vs.css",
                                "/js/prism/prism.css"
                                );
                    });

                options
                    .ScriptBundles
                    .Add(AbpIoBundles.Scripts.Global, bundle =>
                    {
                        bundle.AddBaseBundles(StandardBundles.Scripts.Global);
                    });
            });
        }

        private void ConfigureDatabaseServices(IConfigurationRoot configuration)
        {
            Configure<DbConnectionOptions>(options =>
            {
                options.ConnectionStrings.Default = configuration.GetConnectionString("Default");
            });

            Configure<AbpDbContextOptions>(options =>
            {
                options.Configure(context =>
                {
                    if (context.ExistingConnection != null)
                    {
                        context.DbContextOptions.UseMySql(context.ExistingConnection,
                            mysqlOptions => { mysqlOptions.ServerVersion(new Version(8, 0, 12), ServerType.MySql); });
                    }
                    else
                    {
                        context.DbContextOptions.UseMySql(context.ConnectionString,
                            mysqlOptions => { mysqlOptions.ServerVersion(new Version(8, 0, 12), ServerType.MySql); });
                    }
                });
            });
        }

        private void ConfigureVirtualFileSystem(IHostingEnvironment hostingEnvironment)
        {
            if (hostingEnvironment.IsDevelopment())
            {
                Configure<VirtualFileSystemOptions>(options =>
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<AbpUiModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}..{0}framework{0}src{0}Volo.Abp.UI", Path.DirectorySeparatorChar)));
                    options.FileSets.ReplaceEmbeddedByPhysical<AbpAspNetCoreMvcUiModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}..{0}framework{0}src{0}Volo.Abp.AspNetCore.Mvc.UI", Path.DirectorySeparatorChar)));
                    options.FileSets.ReplaceEmbeddedByPhysical<AbpAspNetCoreMvcUiBootstrapModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}..{0}framework{0}src{0}Volo.Abp.AspNetCore.Mvc.UI.Bootstrap", Path.DirectorySeparatorChar)));
                    options.FileSets.ReplaceEmbeddedByPhysical<AbpAspNetCoreMvcUiThemeSharedModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}..{0}framework{0}src{0}Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared", Path.DirectorySeparatorChar)));
                    options.FileSets.ReplaceEmbeddedByPhysical<DocsDomainModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}..{0}modules{0}docs{0}src{0}Volo.Docs.Domain", Path.DirectorySeparatorChar)));
                    options.FileSets.ReplaceEmbeddedByPhysical<DocsWebModule>(Path.Combine(hostingEnvironment.ContentRootPath,    string.Format("..{0}..{0}..{0}modules{0}docs{0}src{0}Volo.Docs.Web", Path.DirectorySeparatorChar)));
                    options.FileSets.ReplaceEmbeddedByPhysical<BloggingWebModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}..{0}modules{0}blogging{0}src{0}Volo.Blogging.Web", Path.DirectorySeparatorChar)));
                    options.FileSets.ReplaceEmbeddedByPhysical<AbpAccountWebModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}..{0}modules{0}account{0}src{0}Volo.Abp.Account.Web", Path.DirectorySeparatorChar)));
                });
            }
        }

        private void ConfigureTheme()
        {
            Configure<ThemingOptions>(options =>
            {
                options.Themes.Add<AbpIoTheme>();
                options.DefaultThemeName = AbpIoTheme.Name;
            });
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var app = context.GetApplicationBuilder();
            var env = context.GetEnvironment();

            app.ApplicationServices.GetService<AbpWebSiteDbContext>().Database.Migrate();

            app.ApplicationServices.GetService<ISettingDefinitionManager>().Get(LocalizationSettingNames.DefaultLanguage).DefaultValue = "zh-Hans";

            app.UseCorrelationId();

            app.UseAbpRequestLocalization();

            if (env.IsDevelopment()) 
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseErrorPage();
            }

            //Necessary for LetsEncrypt
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), @".well-known")),
                RequestPath = new PathString("/.well-known"),
                ServeUnknownFileTypes = true // serve extensionless file
            });

            app.UseVirtualFiles();

            app.UseAuthentication();

            app.UseMvcWithDefaultRouteAndArea();

            using (var scope = context.ServiceProvider.CreateScope())
            {
                AsyncHelper.RunSync(async () =>
                {
                    await scope.ServiceProvider
                        .GetRequiredService<IIdentityDataSeeder>()
                        .SeedAsync(
                            "1q2w3E*"
                        );

                    await scope.ServiceProvider
                        .GetRequiredService<IPermissionDataSeeder>()
                        .SeedAsync(
                            RolePermissionValueProvider.ProviderName,
                            "admin",
                            IdentityPermissions.GetAll().Union(BloggingPermissions.GetAll())
                        );
                });
            }
        }
    }
}
