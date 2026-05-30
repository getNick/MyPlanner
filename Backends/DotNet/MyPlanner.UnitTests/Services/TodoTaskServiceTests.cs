using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using MyPlanner.Data.Entities.Todo;
using MyPlanner.Data.Repositories;
using MyPlanner.Data.UnitOfWork;
using MyPlanner.Service;
using MyPlanner.Service.Models.Todo.Task;
using NUnit.Framework;

namespace MyPlanner.UnitTests.Services;

[TestFixture]
public class TodoTaskServiceTests
{
    private Mock<IRepository<TodoTask>> _tasksRepoMock;
    private Mock<IUnitOfWork> _unitOfWorkMock;
    private TodoTaskService _sut;

    [SetUp]
    public void SetUp()
    {
        _tasksRepoMock = new Mock<IRepository<TodoTask>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _unitOfWorkMock.Setup(u => u.Tasks).Returns(_tasksRepoMock.Object);
        _sut = new TodoTaskService(_unitOfWorkMock.Object);
    }

    #region CreateAsync Tests

    [Test]
    public async Task CreateAsync_ShouldCreateTaskAndReturnGuid()
    {
        // Arrange
        var expectedTitle = "New Task";
        var expectedListId = Guid.NewGuid();
        var createdTask = new TodoTask
        {
            Id = Guid.NewGuid(),
            Title = expectedTitle,
            Description = string.Empty,
            IsComplete = false,
            ListId = expectedListId,
            Sessions = new List<TodoTaskSession>()
        };

        _tasksRepoMock.Setup(r => r.Create(It.IsAny<TodoTask>()))
                       .Returns((TodoTask entity) => { entity.Id = createdTask.Id; return entity; });

        var request = new CreateTaskRequest(expectedTitle, expectedListId);

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        Assert.That(result, Is.EqualTo(createdTask.Id));
        _tasksRepoMock.Verify(r => r.Create(It.Is<TodoTask>(t =>
            t.Title == expectedTitle &&
            t.ListId == expectedListId &&
            string.IsNullOrEmpty(t.Description) &&
            !t.IsComplete)), Times.Once);
    }

    [Test]
    public async Task CreateAsync_ShouldSetDefaultValues()
    {
        // Arrange
        var expectedTitle = "New Task";
        var expectedListId = Guid.NewGuid();
        var createdTask = new TodoTask
        {
            Id = Guid.NewGuid(),
            Title = expectedTitle,
            Description = string.Empty,
            IsComplete = false,
            ListId = expectedListId,
            Sessions = new List<TodoTaskSession>()
        };

        _tasksRepoMock.Setup(r => r.Create(It.IsAny<TodoTask>()))
                       .Returns((TodoTask entity) => { entity.Id = createdTask.Id; return entity; });

        var request = new CreateTaskRequest(expectedTitle, expectedListId);

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        Assert.That(result, Is.EqualTo(createdTask.Id));
        _tasksRepoMock.Verify(r => r.Create(It.Is<TodoTask>(t =>
            t.Title == expectedTitle &&
            t.ListId == expectedListId &&
            string.IsNullOrEmpty(t.Description) &&
            !t.IsComplete &&
            t.Sessions != null &&
            t.Sessions.Count == 0)), Times.Once);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task CreateAsync_ShouldReturnEmptyGuid_WhenTitleIsInvalid(string? title)
    {
        // Arrange
        var listId = Guid.NewGuid();
        var request = new CreateTaskRequest(title!, listId);

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        Assert.That(result, Is.EqualTo(Guid.Empty));
        _tasksRepoMock.Verify(r => r.Create(It.IsAny<TodoTask>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.Save(), Times.Never);
    }

    #endregion

    #region GetAllAsync Tests

    [Test]
    public async Task GetAllAsync_ShouldReturnEmptyCollection_WhenNoTasksExist()
    {
        // Arrange
        var listId = Guid.NewGuid();
        _tasksRepoMock.Setup(r => r.Get(It.IsAny<System.Linq.Expressions.Expression<Func<TodoTask, bool>>>()))
                       .Returns(new List<TodoTask>().AsQueryable());

        // Act
        var result = await _sut.GetAllAsync(listId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetAllAsync_ShouldProjectToTaskDetailsResponse_ExcludingSessions()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var task1 = new TodoTask
        {
            Id = Guid.NewGuid(),
            Title = "Task One",
            Description = "Description 1",
            IsComplete = false,
            ListId = listId,
            Sessions = new List<TodoTaskSession>()
        };

        var task2 = new TodoTask
        {
            Id = Guid.NewGuid(),
            Title = "Task Two",
            Description = "Description 2",
            IsComplete = true,
            ListId = listId,
            Sessions = new List<TodoTaskSession>()
        };

        _tasksRepoMock.Setup(r => r.Get(It.IsAny<System.Linq.Expressions.Expression<Func<TodoTask, bool>>>(), null, ""))
                       .Returns(new List<TodoTask> { task1, task2 }.AsQueryable());

        // Act
        var result = await _sut.GetAllAsync(listId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(2));

        var firstResponse = result.First(t => t.Id == task1.Id);
        Assert.That(firstResponse.Title, Is.EqualTo("Task One"));
        Assert.That(firstResponse.Description, Is.EqualTo("Description 1"));
        Assert.That(firstResponse.IsComplete, Is.False);
        Assert.That(firstResponse.ListId, Is.EqualTo(listId));

        // Sessions should NOT be mapped (data isolation / projection optimization)
        Assert.That(firstResponse.Sessions, Is.Empty);

        var secondResponse = result.First(t => t.Id == task2.Id);
        Assert.That(secondResponse.Title, Is.EqualTo("Task Two"));
        Assert.That(secondResponse.IsComplete, Is.True);
    }

    #endregion

    #region GetAsync Tests

    [Test]
    public async Task GetAsync_ShouldReturnTaskWithSessions_WhenFound()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var session1 = new TodoTaskSession
        {
            Id = Guid.NewGuid(),
            Start = DateTime.UtcNow.AddHours(-3),
            End = DateTime.UtcNow.AddHours(-2)
        };

        var session2 = new TodoTaskSession
        {
            Id = Guid.NewGuid(),
            Start = DateTime.UtcNow.AddHours(-1),
            End = null // active session
        };

        var task = new TodoTask
        {
            Id = taskId,
            Title = "Detailed Task",
            Description = "Full description with details",
            IsComplete = false,
            ListId = Guid.NewGuid(),
            Sessions = new List<TodoTaskSession> { session1, session2 }
        };

        _tasksRepoMock.Setup(r => r.Get(
            It.IsAny<System.Linq.Expressions.Expression<Func<TodoTask, bool>>>(),
            It.IsAny<Func<IQueryable<TodoTask>, IOrderedQueryable<TodoTask>>>(),
            It.IsAny<string>()))
            .Returns(new List<TodoTask> { task }.AsQueryable());

        // Act
        var result = await _sut.GetAsync(taskId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(taskId));
        Assert.That(result.Title, Is.EqualTo("Detailed Task"));
        Assert.That(result.Description, Is.EqualTo("Full description with details"));
        Assert.That(result.IsComplete, Is.False);

        // Sessions should be mapped to SessionResponse objects
        Assert.That(result.Sessions, Is.Not.Empty);
        Assert.That(result.Sessions.Count, Is.EqualTo(2));

        var firstSession = result.Sessions.First(s => s.Id == session1.Id);
        var expectedStart1 = new DateTimeOffset(DateTime.SpecifyKind(session1.Start!.Value, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var expectedEnd1 = new DateTimeOffset(DateTime.SpecifyKind(session1.End!.Value, DateTimeKind.Utc)).ToUnixTimeSeconds();
        Assert.That(firstSession.StartTimestamp, Is.EqualTo(expectedStart1));
        Assert.That(firstSession.EndTimestamp, Is.EqualTo(expectedEnd1));

        var secondSession = result.Sessions.First(s => s.Id == session2.Id);
        var expectedStart2 = new DateTimeOffset(DateTime.SpecifyKind(session2.Start!.Value, DateTimeKind.Utc)).ToUnixTimeSeconds();
        Assert.That(secondSession.StartTimestamp, Is.EqualTo(expectedStart2));
        Assert.That(secondSession.EndTimestamp, Is.Null); // active session has no end
    }

    [Test]
    public async Task GetAsync_ShouldReturnNull_WhenNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        _tasksRepoMock.Setup(r => r.Get(
            It.IsAny<System.Linq.Expressions.Expression<Func<TodoTask, bool>>>(),
            It.IsAny<Func<IQueryable<TodoTask>, IOrderedQueryable<TodoTask>>>(),
            It.IsAny<string>()))
            .Returns(new List<TodoTask>().AsQueryable());

        // Act
        var result = await _sut.GetAsync(nonExistentId);

        // Assert
        Assert.That(result, Is.Null);
    }

    #endregion

    #region UpdateAsync Tests

    [Test]
    public async Task UpdateAsync_ShouldUpdateTaskAndReturnTrue_WhenFound()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var existingTask = new TodoTask
        {
            Id = taskId,
            Title = "Old Title",
            Description = "Old Description",
            IsComplete = false,
            ListId = Guid.NewGuid(),
            Sessions = new List<TodoTaskSession>()
        };

        _tasksRepoMock.Setup(r => r.GetById(taskId)).Returns(existingTask);
        _tasksRepoMock.Setup(r => r.Update(It.IsAny<TodoTask>())).Returns(true);

        var updateRequest = new UpdateTaskRequest
        {
            Id = taskId,
            Title = "Updated Title",
            Description = "Updated Description",
            IsComplete = true,
            ListId = Guid.NewGuid()
        };

        // Act
        var result = await _sut.UpdateAsync(updateRequest);

        // Assert
        Assert.That(result, Is.True);

        _tasksRepoMock.Verify(r => r.GetById(taskId), Times.Once);
        _tasksRepoMock.Verify(r => r.Update(It.Is<TodoTask>(t =>
            t.Id == taskId &&
            t.Title == "Updated Title" &&
            t.Description == "Updated Description" &&
            t.IsComplete == true)), Times.Once);
    }

    [Test]
    public async Task UpdateAsync_ShouldReturnFalse_WhenTaskNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        _tasksRepoMock.Setup(r => r.GetById(nonExistentId)).Returns((TodoTask?)null);

        var updateRequest = new UpdateTaskRequest
        {
            Id = nonExistentId,
            Title = "Should Not Be Applied",
            Description = "Should Not Be Applied"
        };

        // Act
        var result = await _sut.UpdateAsync(updateRequest);

        // Assert
        Assert.That(result, Is.False);

        // Verify Update was never called since entity doesn't exist
        _tasksRepoMock.Verify(r => r.Update(It.IsAny<TodoTask>()), Times.Never);
    }

    #endregion

    #region DeleteAsync Tests

    [Test]
    public async Task DeleteAsync_ShouldDeleteAndReturnTrue_WhenFound()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        _tasksRepoMock.Setup(r => r.Delete(taskId)).Returns(true);

        // Act
        var result = await _sut.DeleteAsync(taskId);

        // Assert
        Assert.That(result, Is.True);

        _tasksRepoMock.Verify(r => r.Delete(taskId), Times.Once);
        _unitOfWorkMock.Verify(u => u.Save(), Times.Once);
    }

    [Test]
    public async Task DeleteAsync_ShouldReturnFalse_WhenNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        _tasksRepoMock.Setup(r => r.Delete(nonExistentId)).Returns(false);

        // Act
        var result = await _sut.DeleteAsync(nonExistentId);

        // Assert
        Assert.That(result, Is.False);

        _tasksRepoMock.Verify(r => r.Delete(nonExistentId), Times.Once);
        _unitOfWorkMock.Verify(u => u.Save(), Times.Once);
    }

    #endregion
}
