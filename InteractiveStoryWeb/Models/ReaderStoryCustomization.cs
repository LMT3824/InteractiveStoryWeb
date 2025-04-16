namespace InteractiveStoryWeb.Models
{
    public class ReaderStoryCustomization
    {
        public int Id { get; set; }
        public string UserId { get; set; } 
        public int StoryId { get; set; } 
        public string Name { get; set; }  // [Tên]
        public string FirstPersonPronoun { get; set; } // [XưngHôThứNhất]
        public string SecondPersonPronoun { get; set; } // [XưngHôThứHai]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ApplicationUser User { get; set; }
        public Story Story { get; set; }
    }
}
