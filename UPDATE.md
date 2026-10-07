# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [07/10/2026] - Chuẩn Hóa Cấu Hình gRPC Settings, AI Subsystem & Kết Nối CSDL Tập Trung

- **Chuẩn Hóa File Cấu Hình Mẫu (`appsettings.example.json`)**:
  - Bổ sung khối cấu hình `GrpcSettings` (IdentityServiceUrl: `http://localhost:5156`, ContentServiceUrl: `http://localhost:5250`).
  - Bổ sung cấu hình `AiSettings` (ServiceUrl: `http://localhost:8000`).
  - Loại bỏ các trường cấu hình không sử dụng (`JwtSettings`), đồng bộ khớp 100% với `appsettings.json`.
- **Hỗ Trợ Cơ Chế Đồng Bộ Cấu Hình Tập Trung**:
  - Tương thích công cụ `sync_config.bat` của System-Repo để tự động cấp phát cho dev mới.
- **Kiểm Thử Biên Dịch**:
  - `dotnet build` đạt 100% thành công (0 warning, 0 error).
