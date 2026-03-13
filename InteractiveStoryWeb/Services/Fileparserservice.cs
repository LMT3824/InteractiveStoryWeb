using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using InteractiveStoryWeb.ViewModels;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace InteractiveStoryWeb.Services
{
    public interface IFileParserService
    {
        Task<ImportPreviewViewModel> ParseFileAsync(IFormFile file, int storyId, string storyTitle);
    }

    public class FileParserService : IFileParserService
    {
        public async Task<ImportPreviewViewModel> ParseFileAsync(IFormFile file, int storyId, string storyTitle)
        {
            var extension = Path.GetExtension(file.FileName).ToLower();

            return extension switch
            {
                ".docx" => await ParseDocxAsync(file, storyId, storyTitle),
                ".pdf" => await ParsePdfAsync(file, storyId, storyTitle),
                _ => throw new NotSupportedException("Chỉ hỗ trợ file .docx và .pdf")
            };
        }

        private async Task<ImportPreviewViewModel> ParseDocxAsync(IFormFile file, int storyId, string storyTitle)
        {
            var preview = new ImportPreviewViewModel
            {
                StoryId = storyId,
                StoryTitle = storyTitle
            };

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                stream.Position = 0;

                using (WordprocessingDocument doc = WordprocessingDocument.Open(stream, false))
                {
                    var body = doc.MainDocumentPart.Document.Body;
                    ChapterPreviewModel currentChapter = null;
                    SegmentPreviewModel currentSegment = null;
                    var contentBuilder = new StringBuilder();
                    int consecutiveEmptyParagraphs = 0;

                    foreach (var element in body.Elements())
                    {
                        if (element is Paragraph paragraph)
                        {
                            var paragraphProperties = paragraph.ParagraphProperties;
                            var paragraphStyleId = paragraphProperties?.ParagraphStyleId?.Val?.Value;
                            var text = paragraph.InnerText.Trim();

                            // Phát hiện Chapter: Heading 1 hoặc pattern "Chương X"
                            if (IsChapterHeading(paragraphStyleId, text))
                            {
                                // Lưu segment trước (nếu có)
                                if (currentSegment != null && contentBuilder.Length > 0)
                                {
                                    currentSegment.Content = contentBuilder.ToString().TrimEnd();
                                    contentBuilder.Clear();
                                }

                                // Lưu chapter trước (nếu có)
                                if (currentChapter != null && currentChapter.Segments.Any())
                                {
                                    preview.Chapters.Add(currentChapter);
                                }

                                // Tạo chapter mới
                                currentChapter = new ChapterPreviewModel
                                {
                                    Title = CleanChapterTitle(text)
                                };

                                // KHÔNG tạo segment tự động, đợi phát hiện Heading 2
                                currentSegment = null;
                                consecutiveEmptyParagraphs = 0;
                            }
                            // Phát hiện Segment: BẮT BUỘC phải có Heading 2 hoặc pattern "Đoạn X:"
                            else if (IsSegmentHeading(paragraphStyleId, text))
                            {
                                // Lưu segment trước (nếu có)
                                if (currentSegment != null && contentBuilder.Length > 0)
                                {
                                    currentSegment.Content = contentBuilder.ToString().TrimEnd();
                                    contentBuilder.Clear();
                                }

                                // Tạo segment mới
                                if (currentChapter != null)
                                {
                                    var segmentTitle = ExtractSegmentTitle(text);
                                    currentSegment = new SegmentPreviewModel
                                    {
                                        Title = segmentTitle,
                                        Content = string.Empty // Khởi tạo với empty string
                                    };
                                    currentChapter.Segments.Add(currentSegment);
                                }
                                else
                                {
                                    // Nếu chưa có chapter, tạo chapter mặc định
                                    currentChapter = new ChapterPreviewModel
                                    {
                                        Title = "Chương 1"
                                    };
                                    var segmentTitle = ExtractSegmentTitle(text);
                                    currentSegment = new SegmentPreviewModel
                                    {
                                        Title = segmentTitle,
                                        Content = string.Empty // Khởi tạo với empty string
                                    };
                                    currentChapter.Segments.Add(currentSegment);
                                }
                                consecutiveEmptyParagraphs = 0;
                            }
                            // Xử lý nội dung
                            else
                            {
                                // CHỈ thêm nội dung khi ĐÃ CÓ segment
                                if (currentSegment != null)
                                {
                                    // Nếu là dòng trống
                                    if (string.IsNullOrWhiteSpace(text))
                                    {
                                        consecutiveEmptyParagraphs++;
                                        // Thêm xuống dòng (giữ nguyên số lượng dòng trống)
                                        if (contentBuilder.Length > 0)
                                        {
                                            contentBuilder.AppendLine();
                                        }
                                    }
                                    else
                                    {
                                        // BỎ QUA dòng comment
                                        if (IsCommentLine(text))
                                        {
                                            Console.WriteLine($"Skipping comment: {text}");
                                            continue; // Bỏ qua dòng này
                                        }

                                        // Reset counter
                                        consecutiveEmptyParagraphs = 0;

                                        // Nếu không phải đoạn đầu tiên, thêm xuống dòng
                                        if (contentBuilder.Length > 0)
                                        {
                                            contentBuilder.AppendLine();
                                        }

                                        // Thêm nội dung paragraph với format
                                        contentBuilder.Append(FormatParagraph(paragraph));
                                    }
                                }
                                // Nếu chưa có segment, BỎ QUA
                            }
                        }
                    }

                    // Lưu segment cuối cùng
                    if (currentSegment != null && contentBuilder.Length > 0)
                    {
                        currentSegment.Content = contentBuilder.ToString().TrimEnd();
                    }
                    else if (currentSegment != null)
                    {
                        // Nếu segment không có nội dung (có thể toàn comment), gán empty string
                        currentSegment.Content = string.Empty;
                    }

                    // Lưu chapter cuối cùng
                    if (currentChapter != null && currentChapter.Segments.Any())
                    {
                        preview.Chapters.Add(currentChapter);
                    }
                }
            }

            // Nếu không tìm thấy chapter nào, báo lỗi rõ ràng
            if (!preview.Chapters.Any())
            {
                throw new InvalidOperationException(
                    "Không tìm thấy cấu trúc hợp lệ trong file.\n\n" +
                    "Vui lòng đảm bảo:\n" +
                    "- Tiêu đề chương dùng Heading 1 hoặc \"Chương 1:\", \"Chương 2:\"\n" +
                    "- Tiêu đề đoạn dùng Heading 2 hoặc \"Đoạn 1:\", \"Đoạn 2:\"\n" +
                    "- Mỗi đoạn phải có tiêu đề riêng biệt"
                );
            }

            return preview;
        }

        private async Task<ImportPreviewViewModel> ParsePdfAsync(IFormFile file, int storyId, string storyTitle)
        {
            var preview = new ImportPreviewViewModel
            {
                StoryId = storyId,
                StoryTitle = storyTitle
            };

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                stream.Position = 0;

                using (PdfDocument document = PdfDocument.Open(stream))
                {
                    Console.WriteLine($"=== BẮT ĐẦU PHÂN TÍCH PDF: {document.NumberOfPages} trang ===");

                    ChapterPreviewModel currentChapter = null;
                    SegmentPreviewModel currentSegment = null;
                    var contentBuilder = new StringBuilder();
                    int lineNumber = 0;
                    double previousYPosition = double.MaxValue; // Theo dõi vị trí Y của dòng trước

                    // Đọc từng trang
                    foreach (var page in document.GetPages())
                    {
                        Console.WriteLine($"\n--- Đang xử lý trang {page.Number} ---");

                        try
                        {
                            var words = page.GetWords();

                            // Group theo Y position và tạo danh sách dòng với thông tin vị trí
                            var lineGroups = words
                                .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 0))
                                .OrderByDescending(g => g.Key)
                                .Select(g => new
                                {
                                    YPosition = g.Key,
                                    Text = string.Join(" ", g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)),
                                    Height = g.Max(w => w.BoundingBox.Height) // Chiều cao của dòng
                                })
                                .ToList();

                            // Xử lý từng dòng
                            for (int i = 0; i < lineGroups.Count; i++)
                            {
                                var currentLine = lineGroups[i];
                                lineNumber++;
                                var trimmedLine = currentLine.Text.Trim();

                                // Tính khoảng cách với dòng trước
                                double gapFromPrevious = 0;
                                if (previousYPosition != double.MaxValue)
                                {
                                    gapFromPrevious = previousYPosition - currentLine.YPosition;
                                }

                                // Phát hiện khoảng cách lớn (ngắt đoạn) - nếu gap > 1.8 lần chiều cao dòng
                                bool isLargeGap = gapFromPrevious > (currentLine.Height * 1.8);

                                if (string.IsNullOrWhiteSpace(trimmedLine))
                                {
                                    // Dòng trống - thêm xuống dòng nếu đang có nội dung
                                    if (currentSegment != null && contentBuilder.Length > 0)
                                    {
                                        contentBuilder.AppendLine();
                                    }
                                    continue;
                                }

                                // Debug: Log một số dòng đầu
                                if (lineNumber <= 30)
                                {
                                    Console.WriteLine($"  Dòng {lineNumber} (gap: {gapFromPrevious:F1}, height: {currentLine.Height:F1}): {trimmedLine.Substring(0, Math.Min(50, trimmedLine.Length))}...");
                                }

                                // Phát hiện Chapter
                                if (IsChapterHeadingPdf(trimmedLine))
                                {
                                    Console.WriteLine($"  ✓ PHÁT HIỆN CHƯƠNG: {trimmedLine}");

                                    // Lưu segment trước
                                    if (currentSegment != null && contentBuilder.Length > 0)
                                    {
                                        currentSegment.Content = contentBuilder.ToString().TrimEnd();
                                        contentBuilder.Clear();
                                        Console.WriteLine($"    → Đã lưu đoạn: {currentSegment.Title}");
                                    }

                                    // Lưu chapter trước
                                    if (currentChapter != null && currentChapter.Segments.Any())
                                    {
                                        preview.Chapters.Add(currentChapter);
                                        Console.WriteLine($"    → Đã lưu chương: {currentChapter.Title} ({currentChapter.Segments.Count} đoạn)");
                                    }

                                    // Tạo chapter mới
                                    currentChapter = new ChapterPreviewModel
                                    {
                                        Title = CleanChapterTitle(trimmedLine)
                                    };
                                    currentSegment = null;
                                    Console.WriteLine($"    → Tạo chương mới: {currentChapter.Title}");

                                    previousYPosition = currentLine.YPosition;
                                    continue;
                                }

                                // Phát hiện Segment
                                if (IsSegmentHeadingPdf(trimmedLine))
                                {
                                    Console.WriteLine($"  ✓ PHÁT HIỆN ĐOẠN: {trimmedLine}");

                                    // Lưu segment trước
                                    if (currentSegment != null && contentBuilder.Length > 0)
                                    {
                                        currentSegment.Content = contentBuilder.ToString().TrimEnd();
                                        contentBuilder.Clear();
                                        Console.WriteLine($"    → Đã lưu đoạn: {currentSegment.Title}");
                                    }

                                    // Tạo segment mới
                                    if (currentChapter == null)
                                    {
                                        // Nếu chưa có chapter, tạo chapter mặc định
                                        currentChapter = new ChapterPreviewModel
                                        {
                                            Title = "Chương 1"
                                        };
                                        Console.WriteLine($"    → Tạo chương mặc định");
                                    }

                                    var segmentTitle = ExtractSegmentTitle(trimmedLine);
                                    currentSegment = new SegmentPreviewModel
                                    {
                                        Title = segmentTitle,
                                        Content = string.Empty
                                    };
                                    currentChapter.Segments.Add(currentSegment);
                                    Console.WriteLine($"    → Tạo đoạn mới: {segmentTitle}");

                                    previousYPosition = currentLine.YPosition;
                                    continue;
                                }

                                // Nội dung
                                if (currentSegment != null)
                                {
                                    // BỎ QUA dòng comment
                                    if (IsCommentLine(trimmedLine))
                                    {
                                        if (lineNumber <= 30)
                                        {
                                            Console.WriteLine($"    → BỎ QUA comment: {trimmedLine.Substring(0, Math.Min(50, trimmedLine.Length))}");
                                        }
                                        previousYPosition = currentLine.YPosition;
                                        continue;
                                    }

                                    // Nếu có khoảng cách LỚN (> 1.8x height), xuống dòng (ngắt đoạn văn)
                                    if (isLargeGap && contentBuilder.Length > 0)
                                    {
                                        contentBuilder.AppendLine();
                                        contentBuilder.AppendLine(); // Thêm 2 xuống dòng để tạo khoảng cách

                                        if (lineNumber <= 30)
                                        {
                                            Console.WriteLine($"    → Phát hiện khoảng cách lớn, thêm xuống dòng");
                                        }
                                    }
                                    // Nếu gap bình thường (cùng đoạn văn)
                                    else if (contentBuilder.Length > 0)
                                    {
                                        var lastText = contentBuilder.ToString().TrimEnd();
                                        var lastChar = lastText.Length > 0 ? lastText[lastText.Length - 1] : ' ';

                                        // Chỉ xuống dòng nếu câu kết thúc bằng dấu câu HOẶC là dấu ngoặc kép đóng
                                        // KHÔNG xuống dòng cho các dòng bình thường trong đoạn
                                        if ((lastChar == '.' || lastChar == '!' || lastChar == '?') &&
                                            gapFromPrevious > currentLine.Height * 1.1)
                                        {
                                            contentBuilder.AppendLine();
                                        }
                                        else
                                        {
                                            // Nối tiếp dòng bằng khoảng trắng (các dòng trong cùng đoạn văn)
                                            contentBuilder.Append(" ");
                                        }
                                    }

                                    contentBuilder.Append(trimmedLine);
                                }
                                else if (lineNumber <= 30)
                                {
                                    Console.WriteLine($"    → BỎ QUA (chưa có đoạn)");
                                }

                                previousYPosition = currentLine.YPosition;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  ! LỖI khi xử lý trang {page.Number}: {ex.Message}");

                            // Fallback: Sử dụng page.Text với phát hiện dòng trống
                            var pageText = page.Text;
                            var fallbackLines = pageText.Split(new[] { '\n', '\r' }, StringSplitOptions.None);

                            int consecutiveEmptyLines = 0;

                            foreach (var line in fallbackLines)
                            {
                                var trimmedLine = line.Trim();

                                // Đếm số dòng trống liên tiếp
                                if (string.IsNullOrWhiteSpace(trimmedLine))
                                {
                                    consecutiveEmptyLines++;

                                    // Nếu có 2+ dòng trống liên tiếp, thêm xuống dòng
                                    if (consecutiveEmptyLines >= 2 && currentSegment != null && contentBuilder.Length > 0)
                                    {
                                        contentBuilder.AppendLine();
                                        contentBuilder.AppendLine();
                                    }
                                    continue;
                                }

                                consecutiveEmptyLines = 0;
                                lineNumber++;

                                if (IsChapterHeadingPdf(trimmedLine))
                                {
                                    if (currentSegment != null && contentBuilder.Length > 0)
                                    {
                                        currentSegment.Content = contentBuilder.ToString().TrimEnd();
                                        contentBuilder.Clear();
                                    }

                                    if (currentChapter != null && currentChapter.Segments.Any())
                                    {
                                        preview.Chapters.Add(currentChapter);
                                    }

                                    currentChapter = new ChapterPreviewModel
                                    {
                                        Title = CleanChapterTitle(trimmedLine)
                                    };
                                    currentSegment = null;
                                }
                                else if (IsSegmentHeadingPdf(trimmedLine))
                                {
                                    if (currentSegment != null && contentBuilder.Length > 0)
                                    {
                                        currentSegment.Content = contentBuilder.ToString().TrimEnd();
                                        contentBuilder.Clear();
                                    }

                                    if (currentChapter == null)
                                    {
                                        currentChapter = new ChapterPreviewModel { Title = "Chương 1" };
                                    }

                                    var segmentTitle = ExtractSegmentTitle(trimmedLine);
                                    currentSegment = new SegmentPreviewModel
                                    {
                                        Title = segmentTitle,
                                        Content = string.Empty
                                    };
                                    currentChapter.Segments.Add(currentSegment);
                                }
                                else if (currentSegment != null)
                                {
                                    // BỎ QUA comment
                                    if (IsCommentLine(trimmedLine))
                                    {
                                        continue;
                                    }

                                    if (contentBuilder.Length > 0)
                                    {
                                        contentBuilder.AppendLine();
                                    }
                                    contentBuilder.Append(trimmedLine);
                                }
                            }
                        }

                        // Reset position cho trang mới
                        previousYPosition = double.MaxValue;
                    }

                    // Lưu segment cuối
                    if (currentSegment != null && contentBuilder.Length > 0)
                    {
                        currentSegment.Content = contentBuilder.ToString().TrimEnd();
                        Console.WriteLine($"→ Lưu đoạn cuối: {currentSegment.Title}");
                    }

                    // Lưu chapter cuối
                    if (currentChapter != null && currentChapter.Segments.Any())
                    {
                        preview.Chapters.Add(currentChapter);
                        Console.WriteLine($"→ Lưu chương cuối: {currentChapter.Title} ({currentChapter.Segments.Count} đoạn)");
                    }

                    Console.WriteLine($"\n=== HOÀN THÀNH: {preview.Chapters.Count} chương, tổng {preview.Chapters.Sum(c => c.Segments.Count)} đoạn ===");
                }
            }

            if (!preview.Chapters.Any())
            {
                throw new InvalidOperationException(
                    "Không tìm thấy cấu trúc hợp lệ trong file PDF.\n\n" +
                    "Vui lòng đảm bảo:\n" +
                    "- Tiêu đề chương: \"Chương 1\", \"Chương 2\" (có thể có hoặc không có dấu :)\n" +
                    "- Tiêu đề đoạn: \"Đoạn 1\", \"Đoạn 2\" (có thể có hoặc không có dấu :)\n" +
                    "- File PDF phải là text-based (không phải ảnh scan)\n" +
                    "- File được tạo từ Word, Google Docs hoặc editor có cấu trúc rõ ràng\n\n" +
                    "MẸO: Nếu có thể, hãy dùng file .docx thay vì PDF để có kết quả tốt nhất!"
                );
            }

            return preview;
        }

        // ========== HÀM HỖ TRỢ CHO PDF ==========

        private bool IsChapterHeadingPdf(string text)
        {
            var chapterPatterns = new[]
            {
                @"^Chương\s+\d+\s*:?\s*$",                    // "Chương 1" hoặc "Chương 1:"
                @"^Chương\s+\d+\s*:?\s*.{1,100}$",           // "Chương 1: Tiêu đề"
                @"^CHƯƠNG\s+\d+\s*:?\s*$",                    // "CHƯƠNG 1"
                @"^CHƯƠNG\s+\d+\s*:?\s*.{1,100}$",           // "CHƯƠNG 1: Tiêu đề"
                @"^Chapter\s+\d+\s*:?\s*",                    // "Chapter 1"
                @"^CHAPTER\s+\d+\s*:?\s*",                    // "CHAPTER 1"
                @"^Chương\s+[IVXLCDM]+\s*:?\s*",             // "Chương I", "Chương II"
                @"^CHƯƠNG\s+[IVXLCDM]+\s*:?\s*",             // "CHƯƠNG I"
            };

            return chapterPatterns.Any(pattern => Regex.IsMatch(text.Trim(), pattern, RegexOptions.IgnoreCase));
        }

        private bool IsSegmentHeadingPdf(string text)
        {
            // Pattern linh hoạt hơn cho PDF
            var segmentPatterns = new[]
            {
                @"^Đoạn\s+\d+\s*:?\s*$",                     // "Đoạn 1" hoặc "Đoạn 1:"
                @"^Đoạn\s+\d+\s*:?\s*.{1,50}$",             // "Đoạn 1: Tiêu đề ngắn"
                @"^ĐOẠN\s+\d+\s*:?\s*$",                     // "ĐOẠN 1"
                @"^ĐOẠN\s+\d+\s*:?\s*.{1,50}$",             // "ĐOẠN 1: Tiêu đề"
                @"^Phần\s+\d+\s*:?\s*",                      // "Phần 1"
                @"^PHẦN\s+\d+\s*:?\s*",                      // "PHẦN 1"
                @"^Segment\s+\d+\s*:?\s*",                   // "Segment 1"
                @"^\d+\.\s*",                                // "1. ", "2. "
            };

            return segmentPatterns.Any(pattern => Regex.IsMatch(text.Trim(), pattern, RegexOptions.IgnoreCase));
        }

        // ========== HÀM HỖ TRỢ CHO DOCX ==========

        private bool IsChapterHeading(string styleId, string text)
        {
            // Kiểm tra style Heading 1
            if (styleId != null && (styleId.Contains("Heading1") || styleId.Contains("Title")))
                return true;

            // Kiểm tra pattern text
            var chapterPatterns = new[]
            {
                @"^Chương\s+\d+\s*:?",
                @"^CHƯƠNG\s+\d+\s*:?",
                @"^Chapter\s+\d+\s*:?",
                @"^CHAPTER\s+\d+\s*:?",
                @"^Chương\s+[IVXLCDM]+\s*:?",
                @"^CHƯƠNG\s+[IVXLCDM]+\s*:?",
            };

            return chapterPatterns.Any(pattern => Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase));
        }

        private bool IsSegmentHeading(string styleId, string text)
        {
            // Kiểm tra style Heading 2 hoặc Heading 3
            if (styleId != null && (styleId.Contains("Heading2") || styleId.Contains("Heading3")))
                return true;

            // Kiểm tra pattern text - YÊU CẦU có dấu ":" cho DOCX
            var segmentPatterns = new[]
            {
                @"^Đoạn\s+\d+:",
                @"^ĐOẠN\s+\d+:",
                @"^Phần\s+\d+:",
                @"^PHẦN\s+\d+:",
                @"^Segment\s+\d+:",
                @"^\d+\.",
            };

            return segmentPatterns.Any(pattern => Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase));
        }

        private string ExtractSegmentTitle(string text)
        {
            // Loại bỏ dấu ":" ở cuối nếu có
            text = text.TrimEnd(':').Trim();

            // Giới hạn độ dài
            if (text.Length > 100)
            {
                text = text.Substring(0, 97) + "...";
            }

            return text;
        }

        private string CleanChapterTitle(string title)
        {
            // Loại bỏ ký tự đặc biệt không cần thiết
            title = title.Trim();

            // Giới hạn độ dài
            if (title.Length > 200)
            {
                title = title.Substring(0, 197) + "...";
            }

            return title;
        }

        private string FormatParagraph(Paragraph paragraph)
        {
            var text = paragraph.InnerText;
            var runs = paragraph.Elements<Run>();

            var formattedText = new StringBuilder();

            foreach (var run in runs)
            {
                var runText = run.InnerText;
                var runProperties = run.RunProperties;

                if (runProperties != null)
                {
                    bool isBold = runProperties.Bold != null;
                    bool isItalic = runProperties.Italic != null;

                    if (isBold && isItalic)
                    {
                        formattedText.Append($"***{runText}***");
                    }
                    else if (isBold)
                    {
                        formattedText.Append($"**{runText}**");
                    }
                    else if (isItalic)
                    {
                        formattedText.Append($"*{runText}*");
                    }
                    else
                    {
                        formattedText.Append(runText);
                    }
                }
                else
                {
                    formattedText.Append(runText);
                }
            }

            return formattedText.ToString();
        }

        // Kiểm tra xem dòng có phải là comment không
        private bool IsCommentLine(string text)
        {
            var trimmed = text.TrimStart();

            // Hỗ trợ nhiều format comment
            return trimmed.StartsWith("//") ||           // C-style: // comment
                   trimmed.StartsWith("#") ||            // Python-style: # comment
                   trimmed.StartsWith("/*") ||           // C-style multiline: /* comment */
                   trimmed.StartsWith("<!--") ||         // HTML: <!-- comment -->
                   (trimmed.StartsWith("[") &&
                    trimmed.EndsWith("]") &&
                    (trimmed.Contains("Ghi chú") ||
                     trimmed.Contains("Note") ||
                     trimmed.Contains("TODO") ||
                     trimmed.Contains("Chú ý")));        // [Ghi chú: ...], [Note: ...]
        }
    }
}