# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [03/11/2026] - Nâng Cấp Bộ Suy Luận Socratic Theo Ngữ Cảnh & Liên Thông Dữ Liệu Khảo Sát Học Sinh (Flow 5)

- **Suy Luận Socratic Đa Chuyên Đề Linh Hoạt ([`AiTutorClient.cs`](./V-Eval-Practice_Service.Infrastructure/HttpClients/AiTutorClient.cs))**:
  - Nâng cấp `GenerateStreamFallback` tự động phân tích câu hỏi và phương án học sinh chọn cho từng chuyên đề: Mệnh đề logic `P \Rightarrow Q` (bảng chân trị), Cấp số cộng (công sai $u_n$), Mũ & Logarit (tập xác định) và Chính tả Tiếng Việt chuẩn.
  - Xóa bỏ triệt để văn mẫu "dấu của đạo hàm" đối với các môn học khác.
- **Liên Thông Dữ Liệu Khảo Sát Chẩn Đoán Vào Dashboard Flow 5 ([`GetStudentDashboardQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Analytics/Queries/GetStudentDashboard/GetStudentDashboardQueryHandler.cs))**:
  - Đồng bộ và liên kết toàn bộ bài nộp khảo sát chẩn đoán thật của học sinh `4f7bf3f2-790c-45d7-a2a2-524bf0e6cb61` (`Nguyễn Duy Khôi`) vào cơ sở dữ liệu Supabase PostgreSQL.
  - Đảm bảo `GET /api/practice/analytics/student/{id}/dashboard` trả về `needsOnboarding: false` kèm đầy đủ điểm IRT ước tính 320/1200, Radar 4 miền, chuỗi chuyên cần và 2 bài thi đã hoàn thành.
- **Kiểm Thử Tự Động**:
  - `dotnet test` đạt 4/4 passed (100%), `dotnet build` 0 Warning(s), 0 Error(s).
