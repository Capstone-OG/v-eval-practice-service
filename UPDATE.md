# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [17/10/2026] - Đặc Tả Kỹ Thuật & Kế Hoạch Kiến Trúc Core Flow 5 (Learning Analytics & Class Heatmap)

- **Quy Hoạch Kiến Trúc & Thiết Kế Triển Khai Core Flow 5**:
  - Thiết kế kiến trúc Hybrid Aggregation Engine: Fast-path (Write-Through Micro-update $O(1)$) kết hợp Batch-path (Background Sweep Worker $15$ phút) đảm bảo thời gian phản hồi Class Heatmap $< 300\text{ms}$.
  - Hoàn thiện mô hình toán khảo thí định lượng: Cảnh báo đỏ 3 cấp độ ($\ge 40\%$ học sinh yếu hoặc $\overline{P(L_t)} < 0.50$), phát hiện phân hóa 2 cực (Bimodal $\sigma \ge 0.28$), công thức Exam Readiness Index ($R_{\text{exam}} = 0.50 S_\theta + 0.35 C_{\text{BKT}} + 0.15 F_{\text{consistency}}$), và tiêu chuẩn hóa Streak chuyên cần.
  - Thiết kế 4 thực thể CSDL mới trong schema `v_eval_practice`: `ClassSkillHeatmap`, `ClassHeatmapSummary`, `StudentDailyActivity`, `StudentReadinessMetric`.
  - Đặc tả chi tiết bộ 6 REST APIs cho Teacher Heatmap, Student Dashboard, Parent Portal và Campus Director.
  - Cập nhật đồng bộ bộ 3 tài liệu chuẩn `docs/daily.md`, `docs/process.md` và `docs/architecture_acceptance.md`.
