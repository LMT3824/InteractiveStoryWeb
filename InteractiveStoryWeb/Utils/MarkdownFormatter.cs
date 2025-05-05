using InteractiveStoryWeb.Models;
using Markdig;

namespace InteractiveStoryWeb.Utils
{
    public static class MarkdownFormatter
    {
        private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        public static string ToHtml(string markdownContent)
        {
            if (string.IsNullOrEmpty(markdownContent))
                return string.Empty;

            // Chuyển đổi Markdown thành HTML
            return Markdown.ToHtml(markdownContent, _pipeline);
        }

        // Bảo vệ các dấu - không muốn chuyển thành danh sách Markdown
        private static string ProtectDashForDialogue(string content)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimStart();
                // Chỉ áp dụng cho các dòng bắt đầu bằng - hoặc - [space]
                if (line.StartsWith("-") || line.StartsWith("- "))
                {
                    // Nếu dòng có dạng -[Tên], -[XưngHôThứNhất], -[XưngHôThứHai], hoặc - [text], bảo vệ nó
                    if (line.StartsWith("-[") || line.StartsWith("- ["))
                    {
                        // Thêm ký tự \ để ngăn Markdig nhận diện là danh sách
                        lines[i] = "\\" + lines[i];
                    }
                    else if (line.StartsWith("- ") && line.Length > 2)
                    {
                        // Kiểm tra nếu dòng là lời thoại (ví dụ: - Lời thoại), bảo vệ nó
                        lines[i] = "\\" + lines[i];
                    }
                }
            }
            return string.Join("\n", lines);
        }

        public static string FormatContent(string content, ReaderStoryCustomization customization = null)
        {
            // Log nội dung trước khi thay thế
            Console.WriteLine("Before replacement: " + content);

            // Thay thế các placeholder tùy chỉnh nếu có
            if (customization != null)
            {
                content = TextFormatter.ReplaceWithContextualCapitalization(content, "[Tên]", customization.Name);
                content = TextFormatter.ReplaceWithContextualCapitalization(content, "[XưngHôThứNhất]", customization.FirstPersonPronoun);
                content = TextFormatter.ReplaceWithContextualCapitalization(content, "[XưngHôThứHai]", customization.SecondPersonPronoun);
            }

            // Log nội dung sau khi thay thế
            Console.WriteLine("After replacement: " + content);

            // Bảo vệ các dấu - không muốn chuyển thành danh sách
            content = ProtectDashForDialogue(content);

            // Chuyển đổi Markdown thành HTML
            var htmlContent = ToHtml(content);

            // Log nội dung sau khi chuyển đổi Markdown
            Console.WriteLine("After Markdown: " + htmlContent);

            return htmlContent;
        }
    }
}