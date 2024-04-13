using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyPlanner.Data.Migrations
{
    /// <inheritdoc />
    public partial class PagesWithDifferentContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lists_Folder_FolderId",
                table: "Lists");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Lists_ListId",
                table: "Tasks");

            migrationBuilder.DropTable(
                name: "Folder");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Tasks",
                table: "Tasks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Lists",
                table: "Lists");

            migrationBuilder.DropIndex(
                name: "IX_Lists_FolderId",
                table: "Lists");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "Lists");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Lists");

            migrationBuilder.RenameTable(
                name: "Tasks",
                newName: "TodoTasks");

            migrationBuilder.RenameTable(
                name: "Lists",
                newName: "TodoLists");

            migrationBuilder.RenameIndex(
                name: "IX_Tasks_ListId",
                table: "TodoTasks",
                newName: "IX_TodoTasks_ListId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TodoTasks",
                table: "TodoTasks",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TodoLists",
                table: "TodoLists",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Pages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Title = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParentPage = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pages_Pages_ParentPage",
                        column: x => x.ParentPage,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PageContent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Type = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PageId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageContent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PageContent_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Content = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notes_PageContent_Id",
                        column: x => x.Id,
                        principalTable: "PageContent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PageContent_PageId",
                table: "PageContent",
                column: "PageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pages_ParentPage",
                table: "Pages",
                column: "ParentPage");

            migrationBuilder.AddForeignKey(
                name: "FK_TodoLists_PageContent_Id",
                table: "TodoLists",
                column: "Id",
                principalTable: "PageContent",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TodoTasks_TodoLists_ListId",
                table: "TodoTasks",
                column: "ListId",
                principalTable: "TodoLists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TodoLists_PageContent_Id",
                table: "TodoLists");

            migrationBuilder.DropForeignKey(
                name: "FK_TodoTasks_TodoLists_ListId",
                table: "TodoTasks");

            migrationBuilder.DropTable(
                name: "Notes");

            migrationBuilder.DropTable(
                name: "PageContent");

            migrationBuilder.DropTable(
                name: "Pages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TodoTasks",
                table: "TodoTasks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TodoLists",
                table: "TodoLists");

            migrationBuilder.RenameTable(
                name: "TodoTasks",
                newName: "Tasks");

            migrationBuilder.RenameTable(
                name: "TodoLists",
                newName: "Lists");

            migrationBuilder.RenameIndex(
                name: "IX_TodoTasks_ListId",
                table: "Tasks",
                newName: "IX_Tasks_ListId");

            migrationBuilder.AddColumn<Guid>(
                name: "FolderId",
                table: "Lists",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Lists",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Tasks",
                table: "Tasks",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Lists",
                table: "Lists",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Folder",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Title = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Folder", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Lists_FolderId",
                table: "Lists",
                column: "FolderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lists_Folder_FolderId",
                table: "Lists",
                column: "FolderId",
                principalTable: "Folder",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Lists_ListId",
                table: "Tasks",
                column: "ListId",
                principalTable: "Lists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
