import React from "react";
import TodoList from "../../entities/TodoList";
import './TodoFolderView.css'
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faBars, faEllipsis, faChevronLeft, faAngleRight, faAngleDown, } from "@fortawesome/free-solid-svg-icons";

import TodoTask from "../../entities/TodoTask";
import TextInput from "../TextInput/TextInput";
import { Menu } from "@headlessui/react";
import UpdateTask from "../../entities/UpdateTask";
import UpdateList from "../../entities/UpdateList";
import TodoFolder from "../../entities/TodoFolder";
import UpdateFolder from "../../entities/UpdateFolder";


interface TodoFolderViewProps{
    folder: TodoFolder,
    selectedTaskId: string | undefined,
    openSidebar : () => void,
    onAddList: (title: string, folderId: string) => void,
    onUpdateFolder: (updateFolder: UpdateFolder) => void,
    onDeleteTask: (task: TodoTask) => void,
    onUpdateTask: (updateTask: UpdateTask) => void,
    onSelectTask: (task: TodoTask) => void,
}

interface TodoFolderViewState{
  openListIds: string[],
}

export default class TodoFolderView extends React.Component<TodoFolderViewProps,TodoFolderViewState>{
  constructor(props: TodoFolderViewProps){
    super(props);
    this.state = {
      openListIds: []
    };
  }

    onNewListSubmit = (newListTitle: string) =>{
      this.props.onAddList(newListTitle, this.props.folder.id);
    }

    onTitleChanged = (newTitle: string) => {
      let updateFolderModel: UpdateFolder = new UpdateFolder(this.props.folder.id);
      updateFolderModel.title = newTitle;
      this.props.onUpdateFolder(updateFolderModel);
    }
   
    render(): React.ReactNode {
        const listsView : React.ReactNode = this.props.folder.lists.map((list) => this.getListView(list));
        const folderTitle : string = this.props.folder.title;

        return(
            <div className="m-1">
              <div className="flex h-10 items-center">
                <button className="h-8 w-8 rounded hover:bg-slate-200" onClick={this.props.openSidebar}>
                  <FontAwesomeIcon icon={faChevronLeft}/>
                </button>
                
                <TextInput styleName="w-full h-10 p-1 font-bold text-xl"
                            onSubmit={this.onTitleChanged} 
                            placeholderText="Title"
                            value={folderTitle}/>
              </div>

              <TextInput styleName="w-full h-10 m-1 p-1 rounded bg-gray-100 focus:bg-white focus:border-blue-500 focus:border" 
                          placeholderText={`Add list to '${folderTitle}' folder, press Enter to save`}
                          clearTextOnSubmit={true}
                          onSubmit={this.onNewListSubmit}/>
              <ul>
                  {listsView}
              </ul>
            </div>
        );
    }

    getListView(list: TodoList): React.ReactNode {
      const isOpen: boolean = this.getIsListOpen(list);
      const tasks: React.ReactNode = isOpen === false ? null : (
        <ul>
          {list.tasks.map((task) => this.getTaskView(task))}
        </ul> )

      return (
        <li key={list.id} className={`pl-3 pr-1`}>
          <div>
            <button className='h-8 w-8' onClick={(e) => this.toggleIsListOpen(e, list)}>
              <FontAwesomeIcon icon={isOpen ? faAngleDown : faAngleRight} />
            </button>
            <span className="ml-1 font-bold">{list.title}</span>
            <span className="ml-1 text-gray-400">{list.tasks.length}</span>
          </div>
          {tasks}
        </li>
      );
    }

    toggleIsListOpen = (e: React.MouseEvent<HTMLElement>, list: TodoList) => {
      e.stopPropagation();
      this.setState((state:TodoFolderViewState) => {
        const openListIds: string[] =  state.openListIds.includes(list.id) 
          ? state.openListIds.filter((item) => item !== list.id) // exclude
          : [...state.openListIds, list.id]; // add

          return {openListIds : openListIds}
      });
    }

    getIsListOpen = (list: TodoList) : boolean => {
      return this.state.openListIds.includes(list.id);
    }

    onTaskSelection = (e: React.MouseEvent<HTMLElement>, item: TodoTask)=>{
        e.stopPropagation();
        this.props.onSelectTask(item);
    }

    onDelete = (e: React.MouseEvent<HTMLElement>, item: TodoTask)=>{
      e.stopPropagation();
      this.props.onDeleteTask(item);
    }

    onToggleIsComplete = (e: React.ChangeEvent<HTMLInputElement>, task: TodoTask) => {
      e.stopPropagation();
      let updateTaskModel: UpdateTask = new UpdateTask(task.id);
      updateTaskModel.isComplete = !task.isComplete;
      this.props.onUpdateTask(updateTaskModel);
    }

    getTaskView(task: TodoTask): React.ReactNode {
        const isSelected: Boolean = task.id === this.props.selectedTaskId;
        const styleName: string = isSelected ? " item-selected" : "";
        return (
          <li key={task.id} className={`item ${styleName} pl-3 pr-1 justify-between`}>
            <div className='ml-4'>
              <input type="checkbox" checked={task.isComplete} onChange={(e) => this.onToggleIsComplete(e,task)} className="w-4 h-4 rounded"/>
              <span className="ml-1" onClick={(e) => this.onTaskSelection(e,task)} >{task.title}</span>
            </div>
            {this.getTaskOptions(task)}
          </li>
        );
      }

      getTaskOptions(task: TodoTask): React.ReactNode {
        return (
          <Menu as="div" className="relative inline-block text-left">
            <Menu.Button className="h-full w-10 text-zinc-700">
              <FontAwesomeIcon icon={faEllipsis}/>
            </Menu.Button>
            <Menu.Items className="absolute z-10 right-0 mt-2 p-2 origin-top-left divide-y divide-gray-100 rounded-md bg-white shadow-lg focus:outline-none">
              <Menu.Item>
                {({ active }) => (
                  <button className={`item ${active ? 'item-selected' : ''}`} onClick={(e)=> this.onDelete(e, task)}>
                    <span className='px-2'>Delete</span>
                  </button>
                )}
              </Menu.Item>
            </Menu.Items>
          </Menu>
        );
      }
}