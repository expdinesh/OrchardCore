using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using OrchardCore.Environment.Shell;
using OrchardCore.Navigation;

namespace OrchardCore.Scalability
{
    public class AdminMenu : INavigationProvider
    {
        private readonly IStringLocalizer S;

        public AdminMenu(IStringLocalizer<AdminMenu> localizer)
        {
            S = localizer;
        }

        public Task BuildNavigationAsync(string name, NavigationBuilder builder)
        {
            builder.Add(S["Scalability Test"], "0", scalability => scalability
                    .AddClass("scalability").Id("scalability")
                                .Action("Index", "Setup", new { area = "OrchardCore.Scalability" })
                                .LocalNav()
                         );
            return Task.CompletedTask;
        }
    }
}
