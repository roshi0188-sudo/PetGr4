namespace PetSocial.Services
{
    public interface IPetAiService
    {
        bool IsConfigured { get; }
        Task<string> AskCareQuestionAsync(string question, CancellationToken cancellationToken = default);
        Task<string> SuggestDietAsync(string species, string? breed, int? age, decimal? weight, CancellationToken cancellationToken = default);
        Task<PetImageClassification> ClassifyPetImageAsync(IFormFile imageFile, CancellationToken cancellationToken = default);
        Task<ContentModerationResult> CheckContentAsync(string content, IFormFile? imageFile = null, CancellationToken cancellationToken = default);
    }
}
