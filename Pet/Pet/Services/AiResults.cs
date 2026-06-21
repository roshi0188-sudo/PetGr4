namespace PetSocial.Services
{
    public record PetImageClassification(string? Species, string? Breed, string? Note);

    public record ContentModerationResult(bool IsFlagged, bool IsSpam, string Reason);
}
