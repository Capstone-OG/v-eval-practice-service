# CẬP NHẬT TIẾN ĐỘ - V-EVAL PRACTICE SERVICE

## [26/11/2026] - Phân Giải Họ Tên Thí Sinh Cho Hàng Đợi Kiểm Tra Bất Thường, Đóng Học Phí Theo 2 Đợt & Dọn Sạch Seed Hardcode

- **Phân giải họ tên thí sinh bài thi bất thường (`FlaggedMockExamDto.cs`, `GetFlaggedSubmissionsQuery.cs`, `GetMockExamDetailDtos.cs`, `GetMockExamDetailQueryHandler.cs`)**: Tiêm `IIdentityGrpcClient` gọi hàng loạt `GetStudentNamesAsync` để trả kèm `StudentName` và `SubmittedAt` cho cả danh sách bài thi bị gắn cờ lẫn chi tiết hồ sơ giám sát.
- **Thanh toán theo đợt (`PaymentsController.cs`, `CreatePaymentLinkCommand.cs`, `CreatePaymentLinkCommandHandler.cs`)**: Hỗ trợ nhận và lưu `TermId` khi tạo đơn đăng ký & link thanh toán PayOS theo Đợt 1 / Đợt 2.
- **Dọn sạch hardcode (`Program.cs`)**: Gỡ toàn bộ khối seed dữ liệu demo hardcode (~456 dòng) khỏi quá trình khởi động service, chỉ giữ lại phần khởi tạo cấu trúc bảng an toàn.
- **Kiểm thử**: `dotnet build` (`V-Eval-Practice_Service.API`) đạt 0 lỗi, 0 cảnh báo; `dotnet test --no-restore` đạt 20/20 Passed.
