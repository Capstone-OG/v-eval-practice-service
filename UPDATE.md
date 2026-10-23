# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [23/10/2026] - Chuẩn Hóa CSDL Toàn Diện (Master SQL.sql), Xử Lý Dây Đè Bảng Trang 1 & Vẽ Hoàn Thiện Sơ Đồ Khái Niệm Trang 2 (Capstone.drawio)

- **Hợp Nhất Toàn Bộ CSDL Của Hệ Thống (`docs/SQL/SQL.sql`)**:
  - Tích hợp đầy đủ 21 thực thể của phân hệ `v_eval_practice` vào file SQL duy nhất: `Classes`, `ExamSubmissions`, `SubmissionAnswers`, `ProctoringSnapshots`, `ClassEnrollments`, `ClassGroups`, `ClassGroupMembers`, `LiveSessions`, `LiveSessionAttendance`, `LearningProfiles`, `LearningRoadmaps`, `RoadmapNodes`, `StageProgress`, `AdaptiveQuizAttempts`, `MistakeNotebooks`, `ScorePredictions`, `SystemAlerts`, `ClassSkillHeatmaps`, `ClassHeatmapSummaries`, `StudentDailyActivities`, `StudentReadinessMetrics`.
  - Đồng bộ chuẩn hóa toàn bộ khóa ngoại, chỉ mục hiệu năng cao và loại bỏ triệt để các file SQL phân mảnh cũ.
- **Tái Cấu Trúc Sơ Đồ ERD Trang 1 & Xử Lý Dây Đè Bảng Trong `docs/SQL/Capstone.drawio`**:
  - Khắc phục hoàn toàn lỗi dây đè lên bảng bằng cơ chế Highway Corridor Routing và các tiếp điểm biên (`exitX, exitY, entryX, entryY`), kết hợp cầu nối `jumpStyle=arc;jumpSize=6;` chuyên nghiệp.
  - Phân bổ cấu trúc các bảng của `v_eval_practice` vào các package `4. Thi thử & Giám sát liêm chính`, `5. Lớp học, Live & Nhóm vi mô`, `6. Lộ trình thích ứng & Chu trình P-L-A-R`, `8. Dự báo điểm & Cảnh báo sớm BR-04` và `9. Phân tích học tập & Ma trận nhiệt (Analytics)`.
- **Tối Ưu Tuyệt Đối Sơ Đồ Khái Niệm Trang 2 (`Trang-2` Straight Orthogonal ERD)**:
  - Phóng to các ô vuông thực thể (`width = 160..190px, height = 54..75px`) theo lưới 7 cột thoáng đãng, không gom nhóm.
  - Sắp xếp thực thể trọng tâm nhiều dây (`Students`, `Users`, `Skills`, `Classes`, `Questions`) trên trục chính, các bảng vệ tinh ít dây tỏa đều ra ngoại vi.
  - Chuyển toàn bộ 77 đường liên kết sang cơ chế **nối thẳng trực giao (Straight Orthogonal, không dây vòng, không detour)** kết hợp tiếp điểm đa điểm và cầu nhảy `jumpStyle=arc`, đảm bảo dây chạy thẳng tắp, không dính chùm và không đè lên nhau.
