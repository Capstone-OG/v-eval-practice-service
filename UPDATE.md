# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [22/11/2026]
- **Mở Khóa Toàn Bộ Tầng Dạng Bài GDPT 2018 (750+ Dạng Bài Lớp 10–12) Khi Sinh Lộ Trình (`GenerateRoadmapCommandHandler.cs`)**:
  - Loại bỏ điều kiện lọc cứng `&& (s.QuestionCount > 0 || s.MaterialId.HasValue)` trong `GenerateRoadmapCommandHandler.cs` vốn làm rơi toàn bộ 612 dạng bài mới đồng bộ từ bộ SGK Chân trời sáng tạo Lớp 10–12 (khiến lộ trình bị kẹt ở ~370 chặng).
  - Đảm bảo khi học sinh bấm `Cập nhật` ➔ `Tạo Lộ Trình Ngay`, lộ trình nạp đầy đủ 100% tầng `PATTERN` từ cây năng lực Content Service gRPC.
- **Kiểm Thử & Biên Dịch**: `dotnet test --no-restore` đạt **20/20 Passed (100%)**.
