using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyPlanner.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddedTaskSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TodoTasksSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Start = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    End = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TodoTaskId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TodoTasksSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TodoTasksSessions_TodoTasks_TodoTaskId",
                        column: x => x.TodoTaskId,
                        principalTable: "TodoTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_TodoTasksSessions_TodoTaskId",
                table: "TodoTasksSessions",
                column: "TodoTaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TodoTasksSessions");
        }
    }
}
