using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace V_Eval_Practice_Service.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoadmapTeacherApprovalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_teacher_approved",
                schema: "v_eval_practice",
                table: "LearningRoadmaps",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "approved_by_teacher_id",
                schema: "v_eval_practice",
                table: "LearningRoadmaps",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                schema: "v_eval_practice",
                table: "LearningRoadmaps",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "teacher_kickoff_note",
                schema: "v_eval_practice",
                table: "LearningRoadmaps",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_teacher_approved",
                schema: "v_eval_practice",
                table: "LearningRoadmaps");

            migrationBuilder.DropColumn(
                name: "approved_by_teacher_id",
                schema: "v_eval_practice",
                table: "LearningRoadmaps");

            migrationBuilder.DropColumn(
                name: "approved_at",
                schema: "v_eval_practice",
                table: "LearningRoadmaps");

            migrationBuilder.DropColumn(
                name: "teacher_kickoff_note",
                schema: "v_eval_practice",
                table: "LearningRoadmaps");
        }
    }
}
