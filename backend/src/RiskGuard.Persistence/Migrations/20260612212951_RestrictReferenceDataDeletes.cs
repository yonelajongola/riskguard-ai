using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskGuard.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestrictReferenceDataDeletes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assessments_Departments_DepartmentId",
                table: "Assessments");

            migrationBuilder.DropForeignKey(
                name: "FK_Assessments_RiskCategories_RiskCategoryId",
                table: "Assessments");

            migrationBuilder.DropForeignKey(
                name: "FK_Risks_Departments_DepartmentId",
                table: "Risks");

            migrationBuilder.AddForeignKey(
                name: "FK_Assessments_Departments_DepartmentId",
                table: "Assessments",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Assessments_RiskCategories_RiskCategoryId",
                table: "Assessments",
                column: "RiskCategoryId",
                principalTable: "RiskCategories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Risks_Departments_DepartmentId",
                table: "Risks",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assessments_Departments_DepartmentId",
                table: "Assessments");

            migrationBuilder.DropForeignKey(
                name: "FK_Assessments_RiskCategories_RiskCategoryId",
                table: "Assessments");

            migrationBuilder.DropForeignKey(
                name: "FK_Risks_Departments_DepartmentId",
                table: "Risks");

            migrationBuilder.AddForeignKey(
                name: "FK_Assessments_Departments_DepartmentId",
                table: "Assessments",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Assessments_RiskCategories_RiskCategoryId",
                table: "Assessments",
                column: "RiskCategoryId",
                principalTable: "RiskCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Risks_Departments_DepartmentId",
                table: "Risks",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
