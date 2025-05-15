namespace InteractiveStoryWeb.Models
{
    public class SupportTicketResponse
    {
        public int Id { get; set; }
        public int SupportTicketId { get; set; }
        public string AdminId { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public SupportTicket SupportTicket { get; set; }
        public ApplicationUser Admin { get; set; }
    }
}
