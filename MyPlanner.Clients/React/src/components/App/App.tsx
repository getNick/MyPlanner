import React from 'react';
import './App.css';
import FoldersList from '../FoldersList/FoldersList';
import TodoFolder from '../../entities/TodoFolder';
import TodoService from '../../services/TodoService';
import TodoList from '../../entities/TodoList';
import TodoListView from '../TodoListView/TodoListView';
import TodoTask from '../../entities/TodoTask';
import UpdateTask from '../../entities/UpdateTask';
import TodoTaskView from '../TodoTaskView/TodoTaskView';
import UpdateList from '../../entities/UpdateList';

interface AppState{
  folders: TodoFolder[] | undefined,
  listsWithoutFolder: TodoList[] | undefined,
  selectedFolderOrList: TodoFolder | TodoList | undefined,
  selectedTask: TodoTask | undefined,
  isSidebarOpen: boolean,
  isTaskBarOpen: boolean,
}

export default class App extends React.Component<{},AppState>{
  constructor(props: {}){
    super(props);
    this.state = {
      folders : undefined,
      listsWithoutFolder: undefined,
      selectedFolderOrList : undefined,
      selectedTask : undefined,
      isSidebarOpen : true,
      isTaskBarOpen : false,
    }
  }

  todoService = new TodoService();

  componentDidMount(): void {
    this.loadAllItems();
  }

  async loadAllItems(){
    const items = await this.todoService.getFolders();
    const selectedItem = items[0];
    this.setState({
      folders : items,
      selectedFolderOrList: selectedItem
    })
  }

  onFolderOrListSelection = async (item: TodoFolder | TodoList) =>{
    this.setIsSidebarOpen(false)
    const selectedItem: TodoFolder | TodoList | undefined = item instanceof TodoFolder
      ? await this.todoService.getFolder(item.id)
      : await this.todoService.getList(item.id);
    this.setState({
      selectedFolderOrList: selectedItem,
      selectedTask: undefined,
    })
  }

  onSelectTask = async (task: TodoTask) =>{
    this.setIsTaskbarOpen(true);
    this.setState({
      selectedTask : task,
    });
  }

  onSelectTaskById = async (taskId: string) => {
    const selectedTask : TodoTask | undefined = await this.todoService.getTask(taskId);
    if(selectedTask !== undefined)
      this.onSelectTask(selectedTask);
  }

  onAddList = async (title: string, folderId: string|null) =>{
    const listId = await this.todoService.createList(title, folderId);
    const items = await this.todoService.getFolders();
    const selectedItem = await this.todoService.getList(listId);
    this.setState({
      folders : items,
      selectedFolderOrList : selectedItem,
    });
  }

  onAddFolder = async (title: string) =>{
    const folderId = await this.todoService.createFolder(title);
    const items = await this.todoService.getFolders();
    const selectedItem = await this.todoService.getFolder(folderId);
    this.setState({
      folders : items,
      selectedFolderOrList : selectedItem,
    })
  }

  onDeleteFolderOrList = async (item: TodoFolder | TodoList) =>{
    const isRemoved: boolean = item instanceof TodoFolder 
    ? await this.todoService.deleteFolder(item.id)
    : await this.todoService.deleteList(item.id);
    if(isRemoved){
      const items = await this.todoService.getFolders();
      this.setState({
        folders : items,
      })
    }
  }

  onUpdateList = async (list: UpdateList) =>{
    const isUpdated = await this.todoService.updateList(list);
    if(isUpdated === false)
      return;

    const listId: string = list.id;
    const selectedListId: string | undefined = this.state.selectedFolderOrList instanceof TodoList
      ? this.state.selectedFolderOrList.id
      : undefined;
    if(selectedListId === listId){
      const selectedList = await this.todoService.getList(listId);
      this.setState({
        selectedFolderOrList : selectedList,
      })
    }
    const items = await this.todoService.getFolders();
    this.setState({
        folders : items,
    })
  }

  onAddTask = async (title: string, listId: string) =>{
    const taskId = await this.todoService.createTask(title, listId);
    const selectedList = await this.todoService.getList(listId);
    this.setState({
      selectedFolderOrList : selectedList,
    })
    await this.onSelectTaskById(taskId);
  }
  
  onDeleteTask = async (task: TodoTask) =>{
    const isRemoved: boolean = await this.todoService.deleteTask(task.id);
    if(isRemoved){
      const selectedList = await this.todoService.getList(task.listId);
      this.setState({
        selectedFolderOrList : selectedList,
      })
    }
  }

  onUpdateTask = async (task: UpdateTask) =>{
    const isUpdated = await this.todoService.updateTask(task);
    if(isUpdated === false)
      return;

    const selectedListId: string | undefined = this.state.selectedFolderOrList instanceof TodoList
      ? this.state.selectedFolderOrList.id
      : undefined;
    if(selectedListId !== undefined){
      const selectedList = await this.todoService.getList(selectedListId);
      this.setState({
        selectedFolderOrList : selectedList,
      })
    }
  }

  setIsSidebarOpen = (isOpen: boolean) => {
    this.setState({
      isSidebarOpen: isOpen
    })
  }

  setIsTaskbarOpen = (isOpen: boolean) => {
    this.setState({
      isTaskBarOpen: isOpen
    })
  }

  render(): React.ReactNode {

    const listView = this.state.selectedFolderOrList instanceof TodoList 
    ? (<TodoListView list={this.state.selectedFolderOrList} 
                      selectedTaskId={this.state.selectedTask?.id}
                      openSidebar={()=> this.setIsSidebarOpen(true)}
                      onUpdateList={this.onUpdateList}
                      onSelectTask={this.onSelectTask}
                      onAddTask={this.onAddTask} 
                      onDeleteTask={this.onDeleteTask} 
                      onUpdateTask={this.onUpdateTask}/>) 
    : undefined;

    const folderContainerStyle: string = this.state.isSidebarOpen ? "" : " -translate-x-full";
    const taskContainerStyle: string = this.state.isTaskBarOpen ? "" : "translate-x-full";

    return (
      <div className="container grid md:grid-flow-col h-screen m-0">
        <div className={`fixed top-0 left-0 h-full w-full overflow-auto bg-slate-200 transition-transform transform ${folderContainerStyle} z-10`}>
          <FoldersList folders={this.state.folders} 
                       listsWithoutFolder={this.state.listsWithoutFolder} 
                       selectedItemId={this.state.selectedFolderOrList?.id}
                       onSelection={this.onFolderOrListSelection}
                       onAddList={this.onAddList}
                       onAddFolder={this.onAddFolder}
                       onDelete={this.onDeleteFolderOrList}/>
        </div>

        <div className='h-full w-full overflow-auto p-1'>
          {listView}
        </div>

        <div className={`fixed top-0 left-0 h-full w-full overflow-auto p-1 transition-transform transform ${taskContainerStyle} bg-white`}>
          <TodoTaskView task={this.state.selectedTask} 
                        closeTaskBar={()=> this.setIsTaskbarOpen(false)}
                        onUpdateTask={this.onUpdateTask}/>
        </div>
      </div>
    );
  }
}