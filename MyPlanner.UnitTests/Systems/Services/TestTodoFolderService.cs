using Moq;
using MyPlanner.Service;
using MyPlanner.Data.UnitOfWork;
using MyPlanner.Data.Repositories;
using MyPlanner.Data.Entities.Todo;

namespace MyPlanner.Service.UnitTests.Systems.Services;

public class TestTodoFolderService
{
    [Test]
    public async Task Create_ShouldGenerateGuid_CallUnitOfWork_ReturnGuid()
    {
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockFolderRepository = new Mock<IRepository<TodoFolder>>();
        mockUnitOfWork.Setup(x => x.Folders).Returns(mockFolderRepository.Object);

        var sut = new TodoFolderService(mockUnitOfWork.Object);
        string newFolderName = "Folder1";
        var createRequest = new CreateFolderModel(newFolderName);

        Guid id = await sut.CreateAsync(createRequest);

        Assert.That(id, Is.Not.EqualTo(Guid.Empty));
        mockFolderRepository.Verify(x=> x.Create(It.Is<TodoFolder>(folder => 
            folder.Id == id &&
            folder.Title == newFolderName
        )), Times.Exactly(1));
    }

    [Test]
    public async Task Create_ShouldReturnGuidEmptyIfTitleIsEmpty()
    {
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockFolderRepository = new Mock<IRepository<TodoFolder>>();
        mockUnitOfWork.Setup(x => x.Folders).Returns(mockFolderRepository.Object);

        var sut = new TodoFolderService(mockUnitOfWork.Object);
        var createRequest = new CreateFolderModel("");

        Guid id = await sut.CreateAsync(createRequest);

        Assert.That(id, Is.EqualTo(Guid.Empty));
        mockFolderRepository.Verify(x=> x.Create(It.IsAny<TodoFolder>()), Times.Never);
    }

    [Test]
    public async Task Delete_ReturnTrueIfObjectWasRemoved(){
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockFolderRepository = new Mock<IRepository<TodoFolder>>();
        mockUnitOfWork.Setup(x => x.Folders).Returns(mockFolderRepository.Object);
        Guid objectGuid = Guid.NewGuid();
        mockFolderRepository.Setup(x=> x.Delete(It.IsAny<Guid>())).Returns(true);

        var sut = new TodoFolderService(mockUnitOfWork.Object);
        bool res = await sut.DeleteAsync(objectGuid);

        Assert.That(res, Is.EqualTo(true));
        mockFolderRepository.Verify(x=> x.Delete(It.Is<Guid>(id => 
            id == objectGuid
        )), Times.Exactly(1));
    }

    [Test]
    public async Task Delete_ReturnFalseIfObjectWasNotRemoved(){
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockFolderRepository = new Mock<IRepository<TodoFolder>>();
        mockUnitOfWork.Setup(x => x.Folders).Returns(mockFolderRepository.Object);
        Guid objectGuid = Guid.NewGuid();
        mockFolderRepository.Setup(x=> x.Delete(It.IsAny<Guid>())).Returns(false);

        var sut = new TodoFolderService(mockUnitOfWork.Object);
        bool res = await sut.DeleteAsync(objectGuid);

        Assert.That(res, Is.EqualTo(false));
        mockFolderRepository.Verify(x=> x.Delete(It.Is<Guid>(id => 
            id == objectGuid
        )), Times.Exactly(1));
    }

    [Test]
    public async Task GetAllAsync(){
        var expectedFolders = FolderFixture.GetTestFolders();

        (var unitOfWork, var folderRepository) = MockUnitOfWork.MockFolderRepository();
        folderRepository.Setup(x=> x.Get()).Returns(expectedFolders.AsQueryable());

        var sut = new TodoFolderService(unitOfWork.Object);
        IReadOnlyList<TodoFolder> res = await sut.GetAllAsync();

        CollectionAssert.AreEqual(expectedFolders, res);
        folderRepository.Verify(x=> x.Get(), Times.Exactly(1));
    }
}
