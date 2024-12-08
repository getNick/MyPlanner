﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MyPlanner.Data.DBContexts;

#nullable disable

namespace MyPlanner.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20241208121906_AddedTaskSession")]
    partial class AddedTaskSession
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.2")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("MyPlanner.Data.Entities.Common.Page", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<Guid?>("ParentPage")
                        .HasColumnType("char(36)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.HasIndex("ParentPage");

                    b.ToTable("Pages");
                });

            modelBuilder.Entity("MyPlanner.Data.Entities.Common.PageContent", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<Guid>("PageId")
                        .HasColumnType("char(36)");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.HasIndex("PageId")
                        .IsUnique();

                    b.ToTable("PageContent");

                    b.UseTptMappingStrategy();
                });

            modelBuilder.Entity("MyPlanner.Data.Entities.Common.PageSharing", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<Guid>("PageId")
                        .HasColumnType("char(36)");

                    b.Property<string>("SharedWithUserId")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.HasIndex("PageId");

                    b.ToTable("PageSharing");
                });

            modelBuilder.Entity("MyPlanner.Data.Entities.Todo.TodoTask", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<bool>("IsComplete")
                        .HasColumnType("tinyint(1)");

                    b.Property<Guid>("ListId")
                        .HasColumnType("char(36)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.HasIndex("ListId");

                    b.ToTable("TodoTasks");
                });

            modelBuilder.Entity("MyPlanner.Data.Entities.Todo.TodoTaskSession", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<DateTime?>("End")
                        .HasColumnType("datetime(6)");

                    b.Property<DateTime?>("Start")
                        .HasColumnType("datetime(6)");

                    b.Property<Guid>("TodoTaskId")
                        .HasColumnType("char(36)");

                    b.HasKey("Id");

                    b.HasIndex("TodoTaskId");

                    b.ToTable("TodoTasksSessions");
                });

            modelBuilder.Entity("MyPlanner.Data.Entities.Notes.Note", b =>
                {
                    b.HasBaseType("MyPlanner.Data.Entities.Common.PageContent");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.ToTable("Notes", (string)null);
                });

            modelBuilder.Entity("MyPlanner.Data.Entities.Todo.TodoList", b =>
                {
                    b.HasBaseType("MyPlanner.Data.Entities.Common.PageContent");

                    b.ToTable("TodoLists", (string)null);
                });

            modelBuilder.Entity("MyPlanner.Data.Entities.Common.Page", b =>
                {
                    b.HasOne("MyPlanner.Data.Entities.Common.Page", null)
                        .WithMany("IncludePages")
                        .HasForeignKey("ParentPage")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("MyPlanner.Data.Entities.Common.PageContent", b =>
                {
                    b.HasOne("MyPlanner.Data.Entities.Common.Page", null)
                        .WithOne("Content")
                        .HasForeignKey("MyPlanner.Data.Entities.Common.PageContent", "PageId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MyPlanner.Data.Entities.Common.PageSharing", b =>
                {
                    b.HasOne("MyPlanner.Data.Entities.Common.Page", null)
                        .WithMany("Sharing")
                        .HasForeignKey("PageId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MyPlanner.Data.Entities.Todo.TodoTask", b =>
                {
                    b.HasOne("MyPlanner.Data.Entities.Todo.TodoList", null)
                        .WithMany("Tasks")
                        .HasForeignKey("ListId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MyPlanner.Data.Entities.Todo.TodoTaskSession", b =>
                {
                    b.HasOne("MyPlanner.Data.Entities.Todo.TodoTask", null)
                        .WithMany("Sessions")
                        .HasForeignKey("TodoTaskId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MyPlanner.Data.Entities.Notes.Note", b =>
                {
                    b.HasOne("MyPlanner.Data.Entities.Common.PageContent", null)
                        .WithOne()
                        .HasForeignKey("MyPlanner.Data.Entities.Notes.Note", "Id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MyPlanner.Data.Entities.Todo.TodoList", b =>
                {
                    b.HasOne("MyPlanner.Data.Entities.Common.PageContent", null)
                        .WithOne()
                        .HasForeignKey("MyPlanner.Data.Entities.Todo.TodoList", "Id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MyPlanner.Data.Entities.Common.Page", b =>
                {
                    b.Navigation("Content");

                    b.Navigation("IncludePages");

                    b.Navigation("Sharing");
                });

            modelBuilder.Entity("MyPlanner.Data.Entities.Todo.TodoTask", b =>
                {
                    b.Navigation("Sessions");
                });

            modelBuilder.Entity("MyPlanner.Data.Entities.Todo.TodoList", b =>
                {
                    b.Navigation("Tasks");
                });
#pragma warning restore 612, 618
        }
    }
}
