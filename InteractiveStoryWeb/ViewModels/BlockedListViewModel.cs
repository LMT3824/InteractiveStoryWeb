namespace InteractiveStoryWeb.ViewModels
{
    public class BlockedListViewModel
    {
        public List<BlockedItemViewModel> BlockedUsers { get; set; }
        public List<BlockedItemViewModel> BlockedStories { get; set; }
    }

    public class BlockedItemViewModel
    {
        public int Id { get; set; } // ID của bản ghi Block
        public string ItemId { get; set; } // BlockedUserId hoặc BlockedStoryId
        public string Type { get; set; } // "User" hoặc "Story"
        public string Name { get; set; } // Tên người dùng hoặc tiêu đề truyện
        public string ImageUrl { get; set; } // URL ảnh đại diện hoặc ảnh bìa
    }
}
