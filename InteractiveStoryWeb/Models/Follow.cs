namespace InteractiveStoryWeb.Models
{
    public class Follow
    {
        public int Id { get; set; }
        public string FollowerId { get; set; }
        public string FollowingId { get; set; }

        public ApplicationUser Follower { get; set; }
        public ApplicationUser Following { get; set; }
    }
}
