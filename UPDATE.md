# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [23/11/2026]
- **Phân Giải Danh Tính Họ Tên Học Sinh & Tên Kỹ Năng Thực Tế Cho Phân Nhóm Học Tập Vi Mô (`AutoPartitionMicroGroupsCommandHandler.cs`, `GetClassMicroGroupsQueryHandler.cs`, `ClassMicroGroupDtos.cs`)**:
  - Mở rộng DTO `ClassGroupMemberDto` bổ sung `Name` và `Mastery` phân giải trực tiếp từ Identity Service gRPC (`_identityGrpcClient.GetStudentNamesAsync`) và `LearningProfiles`.
  - Mở rộng DTO `ClassMicroGroupDto` bổ sung `CommonWeakSkillNames` ánh xạ từ cây kỹ năng Content Service gRPC (`_contentGrpcClient.GetSkillsTreeAsync`), cung cấp sẵn danh sách tên chuyên đề tiếng Việt chuẩn cho client thay vì chỉ có danh sách GUID thô.
  - Cập nhật `AutoPartitionMicroGroupsCommandHandler` và `GetClassMicroGroupsQueryHandler` tiêm phụ thuộc `IIdentityGrpcClient`, `IContentGrpcClient`, `ILearningProfileRepository` để tự động làm giàu dữ liệu thực tế cho từng nhóm vi mô (3–5 học sinh/bàn).
  - Tối ưu hóa hiệu năng truy vấn: gom nhóm toàn bộ `StudentId` trong các nhóm bàn học để truy vấn phân giải họ tên một lần qua gRPC, loại bỏ hiện tượng N+1.
- **Kiểm Thử & Biên Dịch**:
  - `dotnet build`: Hoàn thành 100%, 0 lỗi, 0 cảnh báo cho `V-Eval-Practice_Service.Application.csproj` và `V-Eval-Practice_Service.Infrastructure.csproj`.
