# CẬP NHẬT TIẾN ĐỘ - V-EVAL PRACTICE SERVICE

## [26/11/2026] - Hỗ Trợ Đóng Học Phí Theo 2 Đợt (`TermId`) & Dọn Sạch Seed Hardcode Trong `Program.cs`

- **Thanh toán theo đợt (`PaymentsController.cs`, `CreatePaymentLinkCommand.cs`, `CreatePaymentLinkCommandHandler.cs`)**: Hỗ trợ nhận và lưu `TermId` khi tạo đơn đăng ký & link thanh toán PayOS theo Đợt 1 / Đợt 2.
- **Dọn sạch hardcode (`Program.cs`)**: Gỡ toàn bộ khối seed dữ liệu demo hardcode (~456 dòng) khỏi quá trình khởi động service, chỉ giữ lại phần khởi tạo cấu trúc bảng an toàn.
- **CSDL Supabase**: Chuyển `v_eval_enrollment` sang mô hình 2 đợt học (`Đợt 1` & `Đợt 2`) cho 2 cơ sở và dọn sạch bản ghi hardcode cũ.
