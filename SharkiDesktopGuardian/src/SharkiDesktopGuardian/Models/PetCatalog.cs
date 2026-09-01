namespace SharkiDesktopGuardian.Models;

public sealed record PetDefinition(
    string Id,
    string DisplayName,
    string AtlasResourcePath);

public static class PetCatalog
{
    public const string DefaultId = "sharki";

    public static IReadOnlyList<PetDefinition> All { get; } =
    [
        new(DefaultId, "Sharki", "Assets/spritesheet.png"),
        new("mummy", "Mummy", "Assets/Pets/Mummy/spritesheet.png")
    ];

    public static PetDefinition Resolve(string? id)
    {
        return All.FirstOrDefault(pet => string.Equals(pet.Id, id, StringComparison.OrdinalIgnoreCase))
               ?? All[0];
    }

    public static string NormalizeId(string? id) => Resolve(id).Id;
}
