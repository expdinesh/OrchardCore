using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "ScalabilityTest",
    Author = ManifestConstants.OrchardCoreTeam,
    Website = ManifestConstants.OrchardCoreWebsite,
    Version = ManifestConstants.OrchardCoreVersion,
    Description = "The setup test module is for creating thousands of orchard instances for testings.",
    Dependencies = new[] { "OrchardCore.Recipes" },
    Category = "Infrastructure"
)]
