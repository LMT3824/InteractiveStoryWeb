namespace InteractiveStoryWeb.Utils
{
    public static class TextFormatter
    {
        public static string ReplaceWithContextualCapitalization(string content, string placeholder, string replacement)
        {
            if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(placeholder) || string.IsNullOrEmpty(replacement))
                return content;

            // Chuẩn hóa placeholder để sửa lỗi chính tả
            string normalizedPlaceholder = placeholder switch
            {
                "[XungHôThứNhất]" => "[XưngHôThứNhất]",
                "[XungHôThứHai]" => "[XưngHôThứHai]",
                "[Tên]" => "[Tên]",
                _ => placeholder
            };

            // Tách nội dung thành các đoạn (dựa trên xuống dòng)
            var paragraphs = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < paragraphs.Length; i++)
            {
                var paragraph = paragraphs[i];
                if (string.IsNullOrEmpty(paragraph)) continue;

                // Tách đoạn thành các câu (dựa trên dấu chấm, chấm than, chấm hỏi)
                var sentenceSeparators = new List<string>(); // Lưu dấu câu (bao gồm cả tổ hợp như !?)
                var sentences = new List<string>();
                int startIndex = 0;
                for (int j = 0; j < paragraph.Length; j++)
                {
                    if (paragraph[j] == '.' || paragraph[j] == '!' || paragraph[j] == '?')
                    {
                        int separatorLength = 1;
                        if (j < paragraph.Length - 1 && (paragraph[j + 1] == '!' || paragraph[j + 1] == '?' || paragraph[j + 1] == '.'))
                        {
                            separatorLength = 2; // Xử lý trường hợp như !? hoặc !!
                        }
                        string separator = paragraph.Substring(j, separatorLength);
                        sentenceSeparators.Add(separator);
                        sentences.Add(paragraph.Substring(startIndex, j - startIndex).Trim());
                        j += separatorLength - 1;
                        startIndex = j + 1;
                    }
                }
                if (startIndex < paragraph.Length)
                {
                    sentences.Add(paragraph.Substring(startIndex).Trim());
                }

                for (int j = 0; j < sentences.Count; j++)
                {
                    var sentence = sentences[j];
                    if (string.IsNullOrEmpty(sentence)) continue;

                    // Sử dụng Regex để tìm tất cả vị trí của placeholder trong câu
                    var matches = System.Text.RegularExpressions.Regex.Matches(sentence, @$"(?<!\w){System.Text.RegularExpressions.Regex.Escape(normalizedPlaceholder)}(?!\w)");
                    if (matches.Count == 0) continue;

                    string updatedSentence = sentence;
                    int offset = 0;

                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        // Xác định vị trí của placeholder trong câu
                        int placeholderIndex = match.Index + offset;
                        string beforePlaceholder = updatedSentence.Substring(0, placeholderIndex);
                        string afterPlaceholder = updatedSentence.Substring(placeholderIndex + match.Length);

                        // Viết hoa nếu placeholder ở đầu câu, sau dấu câu kết thúc, sau dấu ngoặc kép mở, hoặc sau dấu -
                        bool shouldCapitalize = placeholderIndex == 0 || (j > 0 && paragraph.StartsWith(sentence, StringComparison.OrdinalIgnoreCase));
                        if (!shouldCapitalize && placeholderIndex > 0)
                        {
                            char previousChar = updatedSentence[placeholderIndex - 1];
                            shouldCapitalize = previousChar == '.' || previousChar == '!' || previousChar == '?' || previousChar == '\n';
                            // Kiểm tra trường hợp sau dấu ngoặc kép mở
                            if (!shouldCapitalize && previousChar == '"')
                            {
                                int quoteIndex = beforePlaceholder.LastIndexOf('"');
                                shouldCapitalize = quoteIndex == 0 || (quoteIndex > 0 && beforePlaceholder[quoteIndex - 1] == '\n');
                            }
                            // Kiểm tra trường hợp sau dấu -
                            if (!shouldCapitalize)
                            {
                                var trimmed = beforePlaceholder.TrimEnd();

                                // Lấy 5 ký tự cuối để kiểm tra (đủ để chứa "\n- T" hoặc "-T")
                                var recent = trimmed.Length >= 5 ? trimmed.Substring(trimmed.Length - 5) : trimmed;

                                // Regex-like check: nằm đầu dòng, có thể là - hoặc - [space]
                                if (recent.EndsWith("\n-") || recent.EndsWith("\n- ") || recent.EndsWith("\r\n-") || recent.EndsWith("\r\n- ") || recent.EndsWith("-") || recent.EndsWith("- "))
                                {
                                    shouldCapitalize = true;
                                }
                            }
                        }

                        // Thay thế placeholder với từ phù hợp
                        string formattedReplacement;
                        if (normalizedPlaceholder == "[Tên]")
                        {
                            // Đối với [Tên], giữ nguyên cách viết hoa/thường do người dùng nhập
                            formattedReplacement = replacement;
                        }
                        else
                        {
                            // Đối với các placeholder khác, áp dụng viết hoa/thường theo ngữ cảnh
                            formattedReplacement = shouldCapitalize
                                ? CapitalizeFirstLetter(replacement)
                                : LowercaseFirstLetter(replacement);
                        }

                        // Thay thế placeholder trong câu
                        updatedSentence = beforePlaceholder + formattedReplacement + afterPlaceholder;
                        offset += formattedReplacement.Length - match.Length;
                    }

                    sentences[j] = updatedSentence;
                }

                // Ghép lại các câu với dấu câu gốc, đảm bảo có dấu cách sau dấu câu
                var resultSentence = new List<string>();
                for (int j = 0; j < sentences.Count; j++)
                {
                    resultSentence.Add(sentences[j]);
                    if (j < sentenceSeparators.Count)
                    {
                        resultSentence.Add(sentenceSeparators[j] + " "); // Thêm dấu cách sau dấu câu
                    }
                }
                paragraphs[i] = string.Join("", resultSentence);
            }

            // Ghép lại các đoạn, giữ nguyên ký tự xuống dòng
            return string.Join("\n", paragraphs);
        }

        private static string CapitalizeFirstLetter(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return char.ToUpper(text[0]) + text.Substring(1).ToLower();
        }

        private static string LowercaseFirstLetter(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return char.ToLower(text[0]) + text.Substring(1).ToLower();
        }
    }
}