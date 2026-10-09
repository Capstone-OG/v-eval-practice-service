# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [09/10/2026] - Nâng Cấp Mô Hình Giảng Dạy Offline: Giới Hạn Sĩ Số Lớp 20 Người, Đánh Số Thứ Tự Tăng Dần & Phân Nhóm Học Tập Vi Mô (3 - 5 Học Sinh)

- **Giới Hạn Trần Sĩ Số Lớp 20 Học Sinh & Tự Động Đánh Số Thứ Tự Lớp Tăng Dần**:
  - Cập nhật logic xếp lớp trong `ClassEnrollmentRepository.EnrollStudentAsync`:
    - Ràng buộc trần sĩ số tối đa `MaxClassCapacity = 20` cho mỗi lớp cấp độ (Foundation, Acceleration, Breakthrough).
    - Tự động kiểm tra số lượng học sinh đang `ENROLLED` trong từng lớp active tại cơ sở.
    - Khi các lớp hiện tại đã đủ 20 học sinh (hoặc chưa có lớp), hệ thống tự động sinh lớp mới với số thứ tự tăng dần chuẩn hóa (`01, 02...`, ví dụ: `"Lớp Nền tảng (Foundation) 01 - Cơ sở Quận 9"`, `"Lớp Nền tảng (Foundation) 02 - Cơ sở Quận 9"`).
  - Bổ sung phương thức `GetEnrollmentsByClassIdAsync` vào `IClassEnrollmentRepository` phục vụ truy vấn danh sách học sinh theo lớp.
- **Mô Hình Thực Thể & CSDL Nhóm Học Tập Vi Mô (Micro Study Groups)**:
  - Bổ sung thực thể Domain `ClassGroup.cs` (`GroupId`, `ClassId`, `GroupName`, `FocusArea`, `CommonWeakSkillIds`, `RecommendedWorksheetTitle`, `AssignedWorksheetId`, `AssignedWorksheetTitle`, `WorksheetAssignedAt`).
  - Bổ sung thực thể Domain `ClassGroupMember.cs` (`GroupMemberId`, `GroupId`, `StudentId`, `JoinedAt`).
  - Đăng ký `DbSet<ClassGroup>` và `DbSet<ClassGroupMember>` trong `PracticeDbContext.cs`, thiết lập quan hệ cascade delete.
  - Cập nhật bootstrap SQL trong `Program.cs` khởi tạo bảng `"ClassGroups"` và `"ClassGroupMembers"`.
  - Tạo repository `IClassGroupRepository` và `ClassGroupRepository`, đăng ký Scoped DI.
- **Động Cơ Phân Cụm Vi Mô Có Ràng Buộc Kích Thước (`ClassMicroClusterer.cs`)**:
  - Hiện thực thuật toán gom cụm đồng nhất (Homogeneous Capacitated Clustering) đảm bảo nghiêm ngặt sĩ số mỗi nhóm vi mô từ 3 đến 5 học sinh (`3 <= Size <= 5`).
  - Tính toán số nhóm $M$ tối ưu dựa trên sĩ số lớp: `⌈N / 5⌉ <= M <= ⌊N / 3⌋`.
  - Phân tích sư phạm nhóm: Tự động phát hiện các kỹ năng yếu chung (`MasteryScore < 0.60`), xác định `FocusArea`, sinh tên nhóm sư phạm (ví dụ: `"Nhóm 01 - Bàn trọng tâm: [Kỹ năng yếu]"`) và tự động đề xuất phiếu bài tập vi mô thích ứng (`RecommendedWorksheetTitle`).
- **Bộ 3 REST APIs Nhóm Học Tập Vi Mô Trên `ClassesController.cs`**:
  - `POST /api/practice/classes/{classId}/micro-groups/auto-partition`: Tự động phân chia học sinh trong lớp thành các nhóm 3 - 5 bạn theo lỗ hổng kiến thức.
  - `GET /api/practice/classes/{classId}/micro-groups`: Lấy danh sách nhóm vi mô, thành viên từng nhóm và đề luyện tập đã phân phối.
  - `POST /api/practice/classes/{classId}/micro-groups/{groupId}/assign-worksheet`: Phân phối đề luyện tập / phiếu bài tập vi mô thích ứng trực tiếp cho nhóm.
- **Kiểm Thử & Hoàn Thiện**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
