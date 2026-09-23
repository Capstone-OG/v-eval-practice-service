using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace V_Eval_Practice_Service.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSchemaVEvalPractice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "v_eval_practice");

            migrationBuilder.CreateTable(
                name: "Classes",
                schema: "v_eval_practice",
                columns: table => new
                {
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    campus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    teacher_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classes", x => x.class_id);
                });

            migrationBuilder.CreateTable(
                name: "ExamSubmissions",
                schema: "v_eval_practice",
                columns: table => new
                {
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    total_score = table.Column<int>(type: "integer", nullable: false),
                    total_correct = table.Column<int>(type: "integer", nullable: false),
                    total_questions = table.Column<int>(type: "integer", nullable: false),
                    total_time_spent_seconds = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    theta_0 = table.Column<double>(type: "double precision", nullable: true),
                    placement_class = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ai_commentary = table.Column<string>(type: "text", nullable: true),
                    enrolled_class_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamSubmissions", x => x.submission_id);
                });

            migrationBuilder.CreateTable(
                name: "LearningProfiles",
                schema: "v_eval_practice",
                columns: table => new
                {
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mastery_score = table.Column<double>(type: "double precision", nullable: false),
                    last_updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningProfiles", x => x.profile_id);
                });

            migrationBuilder.CreateTable(
                name: "ClassEnrollments",
                schema: "v_eval_practice",
                columns: table => new
                {
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    diagnostic_submission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    enrolled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassEnrollments", x => x.enrollment_id);
                    table.ForeignKey(
                        name: "FK_ClassEnrollments_Classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "v_eval_practice",
                        principalTable: "Classes",
                        principalColumn: "class_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubmissionAnswers",
                schema: "v_eval_practice",
                columns: table => new
                {
                    answer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    selected_option = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false),
                    time_spent_seconds = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionAnswers", x => x.answer_id);
                    table.ForeignKey(
                        name: "FK_SubmissionAnswers_ExamSubmissions_submission_id",
                        column: x => x.submission_id,
                        principalSchema: "v_eval_practice",
                        principalTable: "ExamSubmissions",
                        principalColumn: "submission_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassEnrollments_class_id",
                schema: "v_eval_practice",
                table: "ClassEnrollments",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAnswers_submission_id",
                schema: "v_eval_practice",
                table: "SubmissionAnswers",
                column: "submission_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassEnrollments",
                schema: "v_eval_practice");

            migrationBuilder.DropTable(
                name: "LearningProfiles",
                schema: "v_eval_practice");

            migrationBuilder.DropTable(
                name: "SubmissionAnswers",
                schema: "v_eval_practice");

            migrationBuilder.DropTable(
                name: "Classes",
                schema: "v_eval_practice");

            migrationBuilder.DropTable(
                name: "ExamSubmissions",
                schema: "v_eval_practice");
        }
    }
}
