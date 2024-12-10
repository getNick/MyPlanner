using Moq;
using MyPlanner.Data.Entities.Todo;
using MyPlanner.Data.Repositories;
using MyPlanner.Data.UnitOfWork;

namespace MyPlanner.Service.UnitTests;

public static class MockUnitOfWork
{
    public static (Mock<IUnitOfWork> unitOfWork, Mock<IRepository<TodoFolder>>) MockFolderRepository(){
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockFolderRepository = new Mock<IRepository<TodoFolder>>();
        mockUnitOfWork.Setup(x => x.Folders).Returns(mockFolderRepository.Object);
        return (mockUnitOfWork, mockFolderRepository);
    } 
}
