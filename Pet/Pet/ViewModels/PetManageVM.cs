namespace PetSocial.ViewModels
{
    public class PetManageVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Species { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public decimal? Weight { get; set; }
        public string Personality { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerEmail { get; set; } = string.Empty;
        public int ProfileScore { get; set; }
        public bool HasPhoto { get; set; }
        public bool IsComplete { get; set; }
    }
}
