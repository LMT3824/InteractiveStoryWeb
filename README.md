# Website Truyện Tương Tác (Interactive Story Website)

Nền tảng web cho phép người dùng đọc, sáng tác và chia sẻ truyện tương tác dạng phân nhánh (Interactive Story / Choose Your Own Adventure). Người đọc có thể trực tiếp tham gia vào diễn biến câu chuyện thông qua các lựa chọn cá nhân.

> Đồ án chuyên ngành — Nhóm 9  
> Trường Đại học Công Nghệ TP. HCM  
> Giảng viên hướng dẫn: Vũ Thanh Hiền

---

## Thành viên nhóm

| Tên | MSSV | Lớp |
|---|---|---|
| Lương Mỹ Tuyền | 2280603607 | 22DTHC7 |
| Lê Văn Quý | 2280602659 | 22DTHC7 |

---

## Giới thiệu

Khác với các nền tảng truyện phổ biến hiện nay (Wattpad, Webnovel, Medium) chỉ hỗ trợ truyện kể theo một hướng duy nhất, dự án này xây dựng một nền tảng chuyên biệt cho **truyện phân nhánh** — nơi mỗi lựa chọn của người đọc sẽ dẫn đến một diễn biến và kết thúc khác nhau.

Nền tảng hỗ trợ cả hai phía:
- **Tác giả** có thể dễ dàng tạo và quản lý câu chuyện nhiều nhánh với giao diện trực quan.
- **Người đọc** có thể trải nghiệm câu chuyện theo cách tương tác, thậm chí tùy chỉnh tên và xưng hô của nhân vật chính.

---

## Công nghệ sử dụng

| Thành phần | Công nghệ |
|---|---|
| Ngôn ngữ lập trình | C#, HTML, CSS |
| Framework | ASP.NET Core 9.0 (mô hình MVC) |
| IDE | Visual Studio 2022, Visual Studio Code |
| Cơ sở dữ liệu | Microsoft SQL Server 2022 |
| ORM | Entity Framework Core |
| Giao tiếp thời thực (Real-time) | SignalR |
| Xử lý Markdown | Markdig |
| Đọc file DOCX | DocumentFormat.OpenXml |
| Đọc file PDF | PdfPig |
| Quản lý phiên bản | Git / GitHub |

---

## Tính năng chính

### Đọc truyện
- Tìm kiếm và lọc truyện theo thể loại
- Đọc truyện tương tác theo cấu trúc phân nhánh: mỗi đoạn có các lựa chọn dẫn đến đoạn tiếp theo khác nhau
- Tùy chỉnh tên và xưng hô của nhân vật chính (nếu tác giả bật tính năng)
- Highlight và ghi chú trên nội dung truyện
- Lưu lịch sử đọc và tiến trình tự động
- Quản lý thư viện truyện cá nhân

### Sáng tác & Quản lý truyện
- Đăng tải và quản lý truyện với đầy đủ thông tin (tên, mô tả, thể loại, ảnh bìa)
- Quản lý chương và đoạn truyện theo cấu trúc cây phân nhánh
- Mỗi lựa chọn (Choice) liên kết trực tiếp đến một đoạn cụ thể
- Import nội dung truyện từ file DOCX và PDF
- Kiểm soát chế độ riêng tư / công khai theo từng chương và đoạn

### Tương tác cộng đồng
- Bình luận và đánh giá truyện
- Theo dõi / bỏ theo dõi người dùng khác
- Chặn người dùng hoặc truyện
- Báo cáo truyện hoặc bình luận vi phạm
- Gửi yêu cầu hỗ trợ đến quản trị viên
- Nhận thông báo từ hệ thống theo thời gian thực

### Quản trị viên (Admin)
- Gửi thông báo toàn hệ thống
- Quản lý báo cáo vi phạm, xét duyệt và xử lý
- Chặn tài khoản hoặc ẩn truyện vi phạm
- Xử lý yêu cầu hỗ trợ từ người dùng

---

## Cơ sở dữ liệu

Hệ thống sử dụng các thực thể (Entity) chính sau:

- `ApplicationUser` — Tài khoản người dùng
- `Story` — Truyện
- `Chapter` — Chương
- `ChapterSegment` — Đoạn nội dung trong chương
- `Choice` — Lựa chọn phân nhánh
- `Genre` — Thể loại
- `Comment`, `Rating` — Bình luận và đánh giá
- `Library`, `ReadingProgress` — Thư viện và tiến trình đọc
- `ReaderStoryCustomization` — Tùy chỉnh nhân vật chính
- `Block`, `Follow` — Chặn và theo dõi
- `Notification`, `UserNotificationRead` — Thông báo
- `Report`, `SupportTicket`, `SupportTicketResponse` — Báo cáo và hỗ trợ
- `UserHighlights` — Highlight và ghi chú

---

## Cài đặt và chạy dự án

### Yêu cầu môi trường

- [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/)
- Microsoft SQL Server 2022
- Visual Studio 2022 hoặc Visual Studio Code

### Các bước cài đặt

1. **Clone repository**
   ```bash
   git clone <repository-url>
   cd <tên-thư-mục-dự-án>
   ```

2. **Cấu hình chuỗi kết nối cơ sở dữ liệu (Connection String)**

   Mở file `appsettings.json` và cập nhật chuỗi kết nối:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=YOUR_SERVER;Database=InteractiveStoryDB;Trusted_Connection=True;"
   }
   ```

3. **Chạy migration để tạo cơ sở dữ liệu**
   ```bash
   dotnet ef database update
   ```

4. **Chạy ứng dụng**
   ```bash
   dotnet run
   ```

5. Truy cập ứng dụng tại `https://localhost:5001` hoặc `http://localhost:5000`

---

## Hướng phát triển tương lai

- Tăng cường bảo mật hệ thống với các giải pháp nâng cao
- Tối ưu giao diện người dùng (UI/UX) cho đa nền tảng và thiết bị di động
- Kiểm thử hiệu năng (Performance Testing) với lượng dữ liệu lớn và nhiều người dùng đồng thời
- Hoàn thiện bảng điều khiển quản trị viên với phân quyền chi tiết hơn
- Tích hợp trí tuệ nhân tạo (AI) hỗ trợ sáng tác

---

## Tài liệu tham khảo

- [ASP.NET Core Documentation](https://dotnet.microsoft.com/en-us/)
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- [SignalR](https://learn.microsoft.com/vi-vn/aspnet/core/tutorials/signalr)
- [PdfPig](https://github.com/UglyToad/PdfPig)
- [DocumentFormat.OpenXml](https://learn.microsoft.com/en-us/office/open-xml/open-xml-sdk)
- [Choose Your Own Adventure](https://en.wikipedia.org/wiki/Choose_Your_Own_Adventure)
- [Interactive Storytelling](https://en.wikipedia.org/wiki/Interactive_storytelling)