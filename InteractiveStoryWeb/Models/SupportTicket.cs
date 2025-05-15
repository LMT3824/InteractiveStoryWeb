namespace InteractiveStoryWeb.Models
{
    public class SupportTicket
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsResolved { get; set; } = false;

        public ApplicationUser User { get; set; }
        public ICollection<SupportTicketResponse> SupportTicketResponses { get; set; } = new List<SupportTicketResponse>();
    }
}
