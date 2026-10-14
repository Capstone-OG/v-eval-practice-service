# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [14/10/2026] - Triển Khai Liên Thông Tự Động Gán Đề Thích Ứng Cho Nhóm Học Tập Vi Mô (Practice <-> Content Service API 19)

- **Hiện Thực Hóa HTTP Client Liên Thông REST API Content Service**:
  - Tạo interface `IContentHttpClient` (`Application/Common/Interfaces/IContentHttpClient.cs`) và DTOs `CreateAdaptiveQuizRequest`, `CreateAdaptiveQuizResponse`.
  - Triển khai `ContentHttpClient` (`Infrastructure/HttpClients/ContentHttpClient.cs`) đọc linh hoạt endpoint từ cấu hình `ContentSettings:ServiceUrl` hoặc `GrpcSettings:ContentServiceUrl` (`http://localhost:5250`).
  - Đăng ký scoped vào DI Container (`Infrastructure/DependencyInjection.cs`).
- **Nâng Cấp Hợp Đồng Giao Tiếp API Gán Đề Vi Mô**:
  - Cập nhật `AssignWorksheetRequestDto` (`Features/Classes/DTOs/ClassMicroGroupDtos.cs`) với `string? WorksheetId = null`, `string? WorksheetTitle = null`, `int? QuestionCount = 5`.
  - Nâng cấp `AssignGroupWorksheetCommand` và `ClassesController.cs` để nhận diện yêu cầu tự động đóng gói đề.
- **Hoàn Thiện Bộ Xử Lý Tự Động Đóng Gói Đề Cho Nhóm Học Tập (`AssignGroupWorksheetCommandHandler.cs`)**:
  - Tự động phân tích JSON `CommonWeakSkillIds` của nhóm học tập đã phân cụm.
  - Gọi Content Service API 19 (`POST /api/content/exams/quiz`) đóng gói tức thì 5 câu hỏi chuẩn Bloom L1-L3 kèm lời giải bước một KaTeX.
  - Gán `ExamId` làm `AssignedWorksheetId` và `AssignedWorksheetTitle` trực tiếp cho nhóm học tập.
- **Kiểm Thử & Biên Dịch**:
  - Toàn bộ solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
