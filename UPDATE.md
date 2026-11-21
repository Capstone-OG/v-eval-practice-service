# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [21/11/2026] - Hotfix Pha Apply (Pha A): Khắc Phục Triệt Để Hiện Tượng Cấp Phát Sai Môn Học / Sai Dạng Bài Trong Chu Trình PLAR

- **Chuẩn Hóa Truy Vấn Câu Hỏi Thích Ứng ZPD Theo Kỹ Năng Chuyên Đề ([`GetNextQuestionQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Stages/Queries/GetNextQuestion/GetNextQuestionQueryHandler.cs))**:
  - Loại bỏ hoàn toàn fallback GUID `6f3765db-943e-4810-bf74-d6a8bdc215da` (mã miền Toán học, không phải SkillId) khi tra cứu kỹ năng; bổ sung log cảnh báo rõ ràng khi chặng học thiếu `RoadmapNode` hoặc `SkillId`.
  - Trong Pha APPLY (Luyện tập thích ứng IRT ZPD + BKT Mastery), truyền `examId: null` sang Content Service gRPC để hệ thống tập trung 100% vào việc rút ngân hàng câu hỏi thích ứng theo dạng bài `skillId` của chặng học và cây kỹ năng cùng môn học, không bị gò bó vào danh sách câu hỏi cố định của `QuizExamId`.
  - Kết hợp với cơ chế **Subject Boundary Isolation** từ Content Service gRPC để đảm bảo 100% câu hỏi trả về vùng ZPD [0.60, 0.75] và dải thích ứng L1–L4 thuộc đúng dạng bài và đúng môn học của chặng học (Toán học ra đề Toán học, không bị lọt câu hỏi Ngữ văn/Ngoại ngữ).
- **Kiểm Thử & Biên Dịch Sạch Sẽ**:
  - `dotnet build --no-restore` (0 Warning, 0 Error).
  - `dotnet test --no-restore` (**20/20 Passed 100%**).
