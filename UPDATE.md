# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [20/11/2026] - Triển Khai API Hậu Kiểm Liêm Chính Bài Thi Thử (Core Flow 3 - BR-20) & Thẩm Định Sư Phạm Giáo Viên

- **Triển Khai API Truy Vấn Bài Thi Gắn Cờ Liêm Chính ([`MockExamsController.cs`](./V-Eval-Practice_Service.API/Controllers/MockExamsController.cs), [`GetFlaggedSubmissionsQuery.cs`](./V-Eval-Practice_Service.Application/Features/MockExams/Queries/GetFlaggedSubmissions/GetFlaggedSubmissionsQuery.cs), [`GetFlaggedSubmissionsQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/MockExams/Queries/GetFlaggedSubmissions/GetFlaggedSubmissionsQueryHandler.cs), [`FlaggedMockExamDto.cs`](./V-Eval-Practice_Service.Application/Features/MockExams/DTOs/FlaggedMockExamDto.cs))**:
  - Bổ sung endpoint `GET /api/practice/mock-exams/flagged-reviews?classId={classId}` truy vấn toàn bộ các phiên thi thử có dấu hiệu vi phạm liêm chính (`IntegrityStatus` là `FLAGGED_FOR_REVIEW` hoặc `UNDER_REVIEW`, hoặc `TabDepartureCount > 3`).
  - Hỗ trợ lọc theo mã lớp học `classId` hoặc truy vấn toàn trường/cơ sở đào tạo.
- **Mở Rộng Repository & Truy Vấn Dữ Liệu Thời Gian Thực ([`IMockExamRepository.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/IMockExamRepository.cs), [`MockExamRepository.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/MockExamRepository.cs))**:
  - Bổ sung phương thức `GetFlaggedSubmissionsAsync(Guid? classId, CancellationToken ct)` truy vấn trực tiếp bảng `v_eval_practice."MockExamSubmissions"` trên PostgreSQL.
  - Phân giải thông tin bài nộp, số lần rời tab (BR-20), trạng thái liêm chính học thuật và quyết định thẩm duyệt của giáo viên.
- **Kiểm Thử & Nghiệm Thu**:
  - `dotnet build`: Hoàn thành thành công, 0 lỗi, 0 cảnh báo.
