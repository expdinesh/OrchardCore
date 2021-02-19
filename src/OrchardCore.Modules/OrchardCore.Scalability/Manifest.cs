using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "ScalabilityTest",
    Author = "FoodInvites Team",
    Website = "www.foofinvites.com",
    Version = "1.0.0.0",
    Description = "The setup test module is for creating thousands of orchard instances for testings.",
    Dependencies = new[] { "OrchardCore.Recipes" },
    Category = "Scalability"
)]
