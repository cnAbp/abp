using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Geetest.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;
using Volo.Abp.Configuration;
using Volo.AbpWebSite.Templates;
using Volo.Utils.SolutionTemplating;
using Volo.Utils.SolutionTemplating.Building;

namespace Volo.AbpWebSite.Pages
{
    public class TemplatesModel : AbpPageModel
    {
        private readonly SolutionBuilder _solutionBuilder;
        private readonly IConfigurationAccessor _configurationAccessor;
        private readonly IGeetestManager _geetestManager;
        public const string ProjectNameRegEx = @"^[a-zA-Z_][a-zA-Z0-9_]*(\.[a-zA-Z_][a-zA-Z0-9_]*)*$";
        public TemplatesModel(SolutionBuilder solutionBuilder, IConfigurationAccessor configurationAccessor, IGeetestManager geetestManager)
        {
            _solutionBuilder = solutionBuilder;
            _configurationAccessor = configurationAccessor;
            _geetestManager = geetestManager;
        }

        [BindProperty]
        [BindRequired]
        [Required(ErrorMessage = "项目名称不能为空!")]
        public string CompanyAndProjectName { get; set; }

        [BindProperty]
        [BindRequired]
        [Required(ErrorMessage = "请选择项目类型!")]
        public string ProjectType { get; set; }

        [BindProperty]
        public string Version { get; set; } = StandardVersions.LatestStable;

        [BindProperty]
        public DatabaseProvider DatabaseProvider { get; set; } = DatabaseProvider.EntityFrameworkCore;

        [BindProperty]
        [Display(Name = "使用Nuget包替换本地引用.")]
        public bool ReplaceLocalReferencesToNuget { get; set; } = true;

        [BindProperty(Name = "Geetest_Offline")]
        public bool GeetestOffline { get; set; }

        [BindProperty(Name = "Geetest_Challenge")]
        public string GeetestChallenge { get; set; }

        [BindProperty(Name = "Geetest_Validate")]
        public string GeetestValidate { get; set; }

        [BindProperty(Name = "Geetest_Seccode")]
        public string GeetestSeccode { get; set; }

        public void OnGet()
        {

        }

        public async Task<ActionResult> OnPostAsync()
        {
            if (ModelState.IsValid)
            {
                var geetestValidate = await _geetestManager.ValidateAsync(new GeetestValidate
                {
                    Offline = GeetestOffline,
                    Challenge = GeetestChallenge,
                    Validate = GeetestValidate,
                    Seccode = GeetestSeccode
                });

                if (!geetestValidate)
                {
                    ModelState.AddModelError(string.Empty, "请完成人机验证!");
                    return Page();
                }

                var template = CreateTemplateInfo();

                var result = await _solutionBuilder.BuildAsync(
                    template,
                    CompanyAndProjectName,
                    DatabaseProvider,
                    Version,
                    ReplaceLocalReferencesToNuget
                );

                return File(result.ZipContent, "application/zip", result.ProjectName + ".zip");
            }

            return Page();
        }

        private TemplateInfo CreateTemplateInfo()
        {
            switch (ProjectType)
            {
                case "MvcModule":
                    DatabaseProvider = DatabaseProvider.Irrelevant;
                    return new MvcModuleTemplate(_configurationAccessor.Configuration);
                case "Service":
                    DatabaseProvider = DatabaseProvider.Irrelevant;
                    return new ServiceTemplate(_configurationAccessor.Configuration);
                case "MvcApp":
                default:
                    return new MvcApplicationTemplate(_configurationAccessor.Configuration);
            }
        }
    }
}
