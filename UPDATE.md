# Nhật Ký Cập Nhật (Update Log) - Practice Serv## [11/10/2026] - Hợp Nhất Toàn Diện Nhánh Giảng Dạy Offline: Trần Sĩ Số 20 Học Sinh, Đánh Số Tự Động & Phân Nhóm Vi Mô (3 - 5 Học Sinh)

- **Hợp Nhất Toàn Diện Với Nhánh Develop (Core Flow 2 & Core Flow 3)**:
  - Đồng bộ thành công cấu trúc `PracticeDbContext.cs` tích hợp đồng thời `ClassGroups`, `ClassGroupMembers` (Nhóm vi mô) và `MistakeNotebooks` (Sổ tay lỗi sai SM-2).
- **Mô Hình Giảng Dạy Offline & Trần Sĩ Số 20 Học Sinh**:
  - Ràng buộc trần sĩ số tối đa `MaxClassCapacity = 20` cho mỗi lớp cấp độ (Foundation, Acceleration, Breakthrough) tại cơ sở trong `ClassEnrollmentRepository.EnrollStudentAsync`.
  - Tự động sinh lớp mới với số thứ tự tăng dần chuẩn hóa (`01, 02...`) khi các lớp hiện tại đã đủ 20 học sinh.
- **Thuật Toán Phân Cụm Vi Mô & Quản Lý Nhóm Học Tập (3 - 5 Học Sinh)**:
  - Hiện thực `ClassMicroClusterer.cs`: Phân cụm đồng nhất theo điểm nghẽn kiến thức, ràng buộc chặt chẽ 3 - 5 học sinh/nhóm (`3 <= Size <= 5`), phát hiện `FocusArea` và tự động sinh tên nhóm sư phạm kèm phiếu bài tập vi mô.
  - Bộ 3 REST APIs trên `ClassesController.cs`: `POST /api/practice/classes/{classId}/micro-groups/auto-partition`, `GET /api/practice/classes/{classId}/micro-groups`, `POST /api/practice/classes/{classId}/micro-groups/{groupId}/assign-worksheet`.
- **Kiểm Thử & Biên Dịch**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).(nhân `EaseFactor`), và tự động gắn cờ `IsMastered = true` khi đúng liên tiếp 3 lần.
  - 4 API Endpoints trong [`MistakesController.cs`](./V-Eval-Practice_Service.API/Controllers/MistakesController.cs): `GET /mistakes`, `GET /mistakes/daily-review`, `POST /mistakes/{id}/tag-error`, `POST /mistakes/{id}/review-submit`.
- **Kiểm Thử Toàn Trình & Vận Hành Trực Tiếp (Live Services)**:
  - Đã khởi chạy đồng thời Content Service (gRPC 5250) và Practice Service (REST & Swagger 5261).
  - Kiểm thử toàn diện qua PowerShell: Khởi tạo chặng học -> Gọi cứu trợ bốc câu hỏi từ Content Service -> Nộp bài chấm điểm trực tiếp từ Content Service -> Nhận bộ đề biến thể mới ở đợt kế tiếp.
  - Toàn bộ Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
>>>>>>> origin/develop
