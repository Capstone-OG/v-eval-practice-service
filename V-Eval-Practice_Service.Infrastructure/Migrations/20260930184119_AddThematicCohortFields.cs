using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace V_Eval_Practice_Service.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddThematicCohortFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "class_type",
                schema: "v_eval_practice",
                table: "Classes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "cluster_index",
                schema: "v_eval_practice",
                table: "Classes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "domain_code",
                schema: "v_eval_practice",
                table: "Classes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "domain_id",
                schema: "v_eval_practice",
                table: "Classes",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "class_type",
                schema: "v_eval_practice",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "cluster_index",
                schema: "v_eval_practice",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "domain_code",
                schema: "v_eval_practice",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "domain_id",
                schema: "v_eval_practice",
                table: "Classes");
        }
    }
}
