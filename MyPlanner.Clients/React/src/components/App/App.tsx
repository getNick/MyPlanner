import React from 'react';
import './App.css';
import FoldersList from '../FoldersList/FoldersList';
import TodoFolder from '../../entities/TodoFolder';
import TodoService from '../../services/TodoService';
import TodoList from '../../entities/TodoList';

interface AppState{
  folders: TodoFolder[] | undefined,
  listsWithoutFolder: TodoList[] | undefined,
  selectedFolderId: string | undefined,
}

export default class App extends React.Component<{},AppState>{
  constructor(props: {}){
    super(props);
    this.state = {
      folders : undefined,
      listsWithoutFolder: undefined,
      selectedFolderId : undefined,
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
      selectedFolderId: selectedItem.id
    })
  }

  onFolderSelection = (id: string) =>{
    this.setState({
      selectedFolderId: id
    })
  }

  onListSelection = (id: string) =>{
    this.setState({
      selectedFolderId: id
    })
  }

  onAddList = async (title: string, folderId: string|null) =>{
    const listId = await this.todoService.createList(title, folderId);
    const items = await this.todoService.getFolders();

    if(folderId !== null){
      this.setState({
        folders : items,
        selectedFolderId : folderId,
      });
    }else{
      this.setState({
        folders : items,
        selectedFolderId : listId,
      });
    }
  }

  onAddFolder = async (title: string) =>{
    const folderId = await this.todoService.createFolder(title);
    const items = await this.todoService.getFolders();
    this.setState({
      folders : items,
      selectedFolderId : folderId,
    })
  }

  onDeleteFolder = async (id: string) =>{
    const isRemoved: boolean = await this.todoService.deleteFolder(id);
    if(isRemoved){
      const items = await this.todoService.getFolders();
      this.setState({
        folders : items,
      })
    }
  }

  onDeleteList = async (id: string) =>{
    const isRemoved: boolean = await this.todoService.deleteList(id);
    if(isRemoved){
      const items = await this.todoService.getFolders();
      this.setState({
        folders : items,
      })
    }
  }

  render(): React.ReactNode {
    return (
      <div className="container flex h-full m-0">
        <div className='h-full w-60 m-0 bg-slate-200 shadow-lg p-1'>
          <FoldersList folders={this.state.folders} 
                       listsWithoutFolder={this.state.listsWithoutFolder} 
                       selectedItemId={this.state.selectedFolderId}
                       onFolderSelection={this.onFolderSelection}
                       onListSelection={this.onListSelection}
                       onAddList={this.onAddList}
                       onAddFolder={this.onAddFolder}
                       onDeleteFolder={this.onDeleteFolder}
                       onDeleteList={this.onDeleteList}/>
        </div>
      </div>
    );
  }
}