# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [21/11/2026]
- **Hotfix Pha Apply (Pha A): Khắc Phục Triệt Để Hiện Tượng Cấp Phát Sai Môn Học / Sai Dạng Bài Trong Chu Trình PLAR ([`GetNextQuestionQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Stages/Queries/GetNextQuestion/GetNextQuestionQueryHandler.cs))**:
  - Loại bỏ hoàn toàn fallback GUID `6f3765db-943e-4810-bf74-d6a8bdc215da` (mã miền Toán học, không phải SkillId) khi tra cứu kỹ năng; bổ sung log cảnh báo rõ ràng khi chặng học thiếu `RoadmapNode` hoặc `SkillId`.
  - Trong Pha APPLY (Luyện tập thích ứng IRT ZPD + BKT Mastery), truyền `examId: null` sang Content Service gRPC để hệ thống tập trung 100% vào việc rút ngân hàng câu hỏi thích ứng theo dạng bài `skillId` của chặng học và cây kỹ năng cùng môn học.
  - Kết hợp với cơ chế **Subject Boundary Isolation** từ Content Service gRPC đảm bảo 100% câu hỏi trả về vùng ZPD [0.60, 0.75] và dải thích ứng L1–L4 thuộc đúng dạng bài và đúng môn học của chặng học.
- **Đồng Bộ Danh Tính Họ Tên Học Sinh Thực Tế Qua Identity Service gRPC Cho Ma Trận Nhiệt Lớp Học (Class Heatmap Matrix) (`GetClassHeatmapQueryHandler.cs`, `IIdentityGrpcClient.cs`)**:
  - Tiêm phụ thuộc `IIdentityGrpcClient` vào `GetClassHeatmapQueryHandler` và gọi `GetStudentNamesAsync(studentIds)` liên thông Identity Service qua gRPC port 5156.
  - Phân giải trực tiếp 100% họ tên học sinh thực tế từ bảng `v_eval_identity."Users"` trên CSDL PostgreSQL; loại bỏ hoàn toàn chuỗi mã định danh giả lập `Học sinh #{studentId[..6]}`.
  - Tích hợp bộ lọc bỏ qua Cache lỗi thời và lưu lại vào MemoryCache với TTL 20 phút.
- **Kiểm Thử & Biên Dịch**:
  - `dotnet build`: Hoàn thành 100%, 0 lỗi, 0 cảnh báo.
  - `dotnet test`: Toàn bộ **20/20 unit tests Passed 100%**.
