namespace PetSocial.Services
{
    public class OpenAiOptions
    {
        public string? ApiKey { get; set; }
        public string Model { get; set; } = "gpt-4o";
        public string ModerationModel { get; set; } = "omni-moderation-latest";
    }
}
