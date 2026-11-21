# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [21/11/2026]
- **Đồng Bộ Danh Tính Họ Tên Học Sinh Thực Tế Qua Identity Service gRPC Cho Ma Trận Nhiệt Lớp Học (Class Heatmap Matrix) (`GetClassHeatmapQueryHandler.cs`, `IIdentityGrpcClient.cs`)**:
  - Tiêm phụ thuộc `IIdentityGrpcClient` vào `GetClassHeatmapQueryHandler` và gọi `GetStudentNamesAsync(studentIds)` liên thông Identity Service qua gRPC port 5156.
  - Phân giải trực tiếp 100% họ tên học sinh thực tế từ bảng `v_eval_identity."Users"` trên CSDL PostgreSQL: `Nguyễn Hoàng An`, `Trần Ngọc Mai`, `Phạm Tấn Lộc`, `Mai Thành Nhân`, `Trần Thái Thịnh`, `Lê Hoàng Long`.
  - Loại bỏ hoàn toàn chuỗi mã định danh giả lập `Học sinh #{studentId[..6]}` trong danh sách `rows` của bản đồ nhiệt.
  - Tích hợp bộ lọc bỏ qua Cache lỗi thời: tự động phát hiện và làm tươi dữ liệu khi bộ đệm vẫn chứa định danh cũ, sau đó lưu lại vào MemoryCache với TTL 20 phút.
- **Kiểm Thử & Biên Dịch**:
  - `dotnet build`: Hoàn thành 100%, 0 lỗi, 0 cảnh báo.
  - `dotnet test`: Toàn bộ **20/20 unit tests Passed 100%**.
  - Kiểm thử trực tiếp API `GET /api/practice/classes/{classId}/heatmap`: Trả về chuẩn xác 100% họ tên học sinh tiếng Việt trong từng dòng dữ liệu `rows`.
