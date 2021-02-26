using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Abstractions.Setup;
using OrchardCore.Data;
using OrchardCore.Email;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Setup.Services;
using OrchardCore.Scalability.ViewModels;
using Newtonsoft.Json;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using System.Net;

namespace OrchardCore.Scalability.Controllers
{
    public class AdminController : Controller
    {
        private const string defaultAdminName = "admin";
        private const string defaultAdminPassword = "Lostman@123";
        private readonly IClock _clock;
        private readonly ISetupService _setupService;
        private ShellSettings _shellSettings;
        private readonly IShellHost _shellHost;
        private IdentityOptions _identityOptions;
        private readonly IEmailAddressValidator _emailAddressValidator;
        private readonly IEnumerable<DatabaseProvider> _databaseProviders;
        private readonly ILogger _logger;
        private readonly IStringLocalizer S;
        private readonly IShellSettingsManager _shellSettingsManager;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        private int shellCount = 100;

        List<SetupViewModel> shellList = new List<SetupViewModel>();

        public AdminController(
            IClock clock,
            ISetupService setupService,
            ShellSettings shellSettings,
            IShellHost shellHost,
            IOptions<IdentityOptions> identityOptions,
            IEmailAddressValidator emailAddressValidator,
            IEnumerable<DatabaseProvider> databaseProviders,
            IStringLocalizer<AdminController> localizer,
            IShellSettingsManager shellSettingsManager,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<AdminController> logger)
        {
            _clock = clock;
            _setupService = setupService;
            _shellSettings = shellSettings;
            _shellHost = shellHost;
            _identityOptions = identityOptions.Value;
            _emailAddressValidator = emailAddressValidator;
            _databaseProviders = databaseProviders;
            _logger = logger;
            S = localizer;
            _shellSettingsManager = shellSettingsManager;
            _configuration = configuration;
            _environment = environment;
        }


        public async Task<ActionResult> DeployTestTenant()
        {
            await DeployShells(1);
            return View("Deployed");
        }
        public async Task<ActionResult> Deploy10()
        {
            shellCount = 10;
            await DeployShells(shellCount);
            return View("Deployed");
        }

        public async Task<ActionResult> Deploy100()
        {
            shellCount = 100;
            await DeployShells(shellCount);
            return View("Deployed");
        }

        public async Task<ActionResult> Deploy500()
        {
            shellCount = 500;
            await DeployShells(shellCount);
            return View("Deployed");
        }

        public async Task<ActionResult> Deploy1000()
        {
            shellCount = 1000;
            await DeployShells(shellCount);
            return View("Deployed");
        }

        public async Task<ActionResult> Deploy5000()
        {
            shellCount = 5000;
            await DeployShells(shellCount);
            return View("Deployed");
        }

        public async Task<ActionResult> Deploy10000()
        {
            shellCount = 10000;
            await DeployShells(shellCount);
            return View("Deployed");
        }

        public async Task<IActionResult> Index()
        {
            // await _dashboardTenantBootstrapperService.BootstrapTenant();
            return await Task.FromResult(View("Index"));
        }


        //[HttpPost, ActionName("Index")]
        public async Task<bool> GenerateTenants(SetupViewModel model)
        {
            if (!string.IsNullOrWhiteSpace(_shellSettings["Secret"]))
            {
                if (string.IsNullOrEmpty(model.Secret) || !await IsTokenValid(model.Secret))
                {
                    _logger.LogWarning("An attempt to access '{TenantName}' without providing a valid secret was made", _shellSettings.Name);
                    return false;
                }
            }

            model.DatabaseProviders = _databaseProviders;
            model.Recipes = await _setupService.GetSetupRecipesAsync();

            var selectedProvider = model.DatabaseProviders.FirstOrDefault(x => x.Value == model.DatabaseProvider);

            if (!model.DatabaseConfigurationPreset)
            {
                if (selectedProvider != null && selectedProvider.HasConnectionString && String.IsNullOrWhiteSpace(model.ConnectionString))
                {
                    ModelState.AddModelError(nameof(model.ConnectionString), S["The connection string is mandatory for this provider."]);
                }
            }

            if (String.IsNullOrEmpty(model.Password))
            {
                ModelState.AddModelError(nameof(model.Password), S["The password is required."]);
            }

            if (model.Password != model.PasswordConfirmation)
            {
                ModelState.AddModelError(nameof(model.PasswordConfirmation), S["The password confirmation doesn't match the password."]);
            }

            RecipeDescriptor selectedRecipe = null;
            if (!string.IsNullOrEmpty(_shellSettings["RecipeName"]))
            {
                selectedRecipe = model.Recipes.FirstOrDefault(x => x.Name == _shellSettings["RecipeName"]);
                if (selectedRecipe == null)
                {
                    ModelState.AddModelError(nameof(model.RecipeName), S["Invalid recipe."]);
                }
            }
            else if (String.IsNullOrEmpty(model.RecipeName) || (selectedRecipe = model.Recipes.FirstOrDefault(x => x.Name == model.RecipeName)) == null)
            {
                ModelState.AddModelError(nameof(model.RecipeName), S["Invalid recipe."]);
            }

            // Only add additional errors if attribute validation has passed.
            if (!String.IsNullOrEmpty(model.Email) && !_emailAddressValidator.Validate(model.Email))
            {
                ModelState.AddModelError(nameof(model.Email), S["The email is invalid."]);
            }

            if (!String.IsNullOrEmpty(model.UserName) && model.UserName.Any(c => !_identityOptions.User.AllowedUserNameCharacters.Contains(c)))
            {
                ModelState.AddModelError(nameof(model.UserName), S["User name '{0}' is invalid, can only contain letters or digits.", model.UserName]);
            }

            if (!ModelState.IsValid)
            {
                CopyShellSettingsValues(model);
                return false;
            }

            var setupContext = new SetupContext
            {
                ShellSettings = _shellSettings,
                EnabledFeatures = null, // default list,
                Errors = new Dictionary<string, string>(),
                Recipe = selectedRecipe,
                Properties = new Dictionary<string, object>
                {
                    { SetupConstants.SiteName, model.SiteName },
                    { SetupConstants.AdminUsername, model.UserName },
                    { SetupConstants.AdminEmail, model.Email },
                    { SetupConstants.AdminPassword, model.Password },
                    { SetupConstants.SiteTimeZone, model.SiteTimeZone },
                }
            };

            if (!string.IsNullOrEmpty(_shellSettings["ConnectionString"]))
            {
                setupContext.Properties[SetupConstants.DatabaseProvider] = _shellSettings["DatabaseProvider"];
                setupContext.Properties[SetupConstants.DatabaseConnectionString] = _shellSettings["ConnectionString"];
                setupContext.Properties[SetupConstants.DatabaseTablePrefix] = _shellSettings["TablePrefix"];
            }
            else
            {
                setupContext.Properties[SetupConstants.DatabaseProvider] = model.DatabaseProvider;
                setupContext.Properties[SetupConstants.DatabaseConnectionString] = model.ConnectionString;
                setupContext.Properties[SetupConstants.DatabaseTablePrefix] = model.TablePrefix;
            }

            var executionId = await _setupService.SetupAsync(setupContext);

            // Check if a component in the Setup failed
            if (setupContext.Errors.Any())
            {
                foreach (var error in setupContext.Errors)
                {
                    ModelState.AddModelError(error.Key, error.Value);
                }
                return false;
            }

            return true;
        }

        private void CopyShellSettingsValues(SetupViewModel model)
        {
            if (!String.IsNullOrEmpty(_shellSettings["ConnectionString"]))
            {
                model.DatabaseConfigurationPreset = true;
                model.ConnectionString = _shellSettings["ConnectionString"];
            }

            if (!String.IsNullOrEmpty(_shellSettings["RecipeName"]))
            {
                model.RecipeNamePreset = true;
                model.RecipeName = _shellSettings["RecipeName"];
            }

            if (!String.IsNullOrEmpty(_shellSettings["DatabaseProvider"]))
            {
                model.DatabaseConfigurationPreset = true;
                model.DatabaseProvider = _shellSettings["DatabaseProvider"];
            }
            else
            {
                model.DatabaseProvider = model.DatabaseProviders.FirstOrDefault(p => p.IsDefault)?.Value;
            }

            if (!String.IsNullOrEmpty(_shellSettings["Description"]))
            {
                model.Description = _shellSettings["Description"];
            }
        }

        private async Task<bool> IsTokenValid(string token)
        {
            try
            {
                var result = false;

                var shellScope = await _shellHost.GetScopeAsync(ShellHelper.DefaultShellName);

                await shellScope.UsingAsync(scope =>
                {
                    var dataProtectionProvider = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
                    var dataProtector = dataProtectionProvider.CreateProtector("Tokens").ToTimeLimitedDataProtector();

                    var tokenValue = dataProtector.Unprotect(token, out var expiration);

                    if (_clock.UtcNow < expiration.ToUniversalTime())
                    {
                        if (_shellSettings["Secret"] == tokenValue)
                        {
                            result = true;
                        }
                    }

                    return Task.CompletedTask;
                });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in decrypting the token");
            }

            return false;
        }

        private async Task<SetupViewModel> InitSetupViewModel(int count)
        {
            string tenantName = GenerateTenantName() + "_" + count.ToString();
            string connectionString = "TenantSettings:ConnectionString";
            SetupViewModel setupViewModel = new SetupViewModel();
            setupViewModel.DatabaseProvider = "SqlConnection";
            setupViewModel.ConnectionString = _configuration[connectionString];
            setupViewModel.RecipeName = "Agency";
            setupViewModel.SiteName = tenantName;
            setupViewModel.SiteTimeZone = "Asia/Kolkata";
            setupViewModel.Email = tenantName + "@gmail.com";
            setupViewModel.Password = defaultAdminPassword;
            setupViewModel.PasswordConfirmation = defaultAdminPassword;
            setupViewModel.UserName = defaultAdminName;
            await InitializeShellSettings(setupViewModel, tenantName);

            return setupViewModel;
        }

        private async Task InitializeShellSettings(SetupViewModel setupViewModel, string tenantName)
        {
            try
            {
                _shellSettings = new ShellSettings
                {
                    Name = tenantName,
                    RequestUrlPrefix = tenantName,
                    RequestUrlHost = null,   //We will need to support request with Domain Names as well in Future
                    State = TenantState.Uninitialized
                };

                _shellSettings["RecipeName"] = setupViewModel.RecipeName;

                //Time to get the Database settings for the current instance

                _shellSettings["DatabaseProvider"] = setupViewModel.DatabaseProvider;

                _shellSettings["ConnectionString"] = setupViewModel.ConnectionString;
                _shellSettings["TablePrefix"] = tenantName;
                await _shellSettingsManager.SaveSettingsAsync(_shellSettings);
                var shellContext = await _shellHost.GetOrCreateShellContextAsync(_shellSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create shell settings for {tenantName}. {ex}");
            }

        }

        private async Task<bool> DeployShells(int count)
        {
            shellList.Clear();
            _logger.LogDebug($"Start Time for deploying { count} number of tenants.: {DateTime.Now.TimeOfDay}");
            for (int i = 0; i < count; i++)
            {
                var setupModel = await InitSetupViewModel(i);

                bool result = await GenerateTenants(setupModel);

                await Task.Delay(5000);

                if (result)
                {
                    shellList.Add(setupModel);
                }
                else
                {
                    break;
                }
            }
            _logger.LogDebug($"End Time for deploying { count} number of tenants.: {DateTime.Now.TimeOfDay}");

            string folderPath = Path.Combine(_environment.WebRootPath, "Deployments");
            DirectoryInfo info = new DirectoryInfo(folderPath);
            if (!info.Exists)
            {
                info.Create();
            }
            string path = Path.Combine(folderPath, "deployedtenants.json");
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(shellList.Select(x => x.SiteName).ToList());

                //write string to file
                System.IO.File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to write json. {ex}");

            }
            return true;
        }

        public async Task<IActionResult> generateTenantList()
        {
            var shellSettings = await _shellSettingsManager.LoadSettingsAsync();
            string folderPath = Path.Combine(_environment.WebRootPath, "Deployments");
            DirectoryInfo info = new DirectoryInfo(folderPath);
            if (!info.Exists)
            {
                info.Create();
            }
            string path = Path.Combine(folderPath, "tenantlist.json");
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(shellSettings.Select(x => x.RequestUrlPrefix).ToList());
                //write string to file
                System.IO.File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to write json. {ex}");

            }
            return View("TenantsList"); ;
           
            //return true;
        }

        public string GenerateTenantName()
        {
            int length = 8;
            Random random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray()).ToLower();
        }
    }
}
