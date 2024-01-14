import React from "react";
import TodoList from "../../entities/TodoList";
import './TodoListView.css'
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faBars, faEllipsis, faChevronLeft } from "@fortawesome/free-solid-svg-icons";
import TodoTask from "../../entities/TodoTask";
import TextInput from "../TextInput/TextInput";
import { Menu } from "@headlessui/react";
import UpdateTask from "../../entities/UpdateTask";
import UpdateList from "../../entities/UpdateList";

interface TodoListViewProps{
    list: TodoList,
    selectedTaskId: string | undefined,
    openSidebar : () => void,
    onUpdateList: (list: UpdateList) => void,
    onAddTask: (title: string, listId: string) => void,
    onDeleteTask: (task: TodoTask) => void,
    onUpdateTask: (task: UpdateTask) => void,
    onSelectTask: (task: TodoTask) => void,
}

interface TodoListViewState{

}


export default class TodoListView extends React.Component<TodoListViewProps,TodoListViewState>{
    onNewTaskSubmit = (newTaskTitle: string) =>{
      this.props.onAddTask(newTaskTitle, this.props.list.id);
    }

    onTitleChanged = (newTitle: string) => {
      let changeTitleChange: UpdateList = new UpdateList(this.props.list.id);
      changeTitleChange.title = newTitle;
      this.props.onUpdateList(changeTitleChange)
    }

   
    render(): React.ReactNode {
        const tasksView : React.ReactNode = this.props.list.tasks.map((folder) => this.getTaskView(folder));
        const listTitle : string = this.props.list.title;

        return(
            <div className="m-1">
              <div className="flex h-10 items-center">
                <button className="h-8 w-8 rounded hover:bg-slate-200" onClick={this.props.openSidebar}>
                  <FontAwesomeIcon icon={faChevronLeft}/>
                </button>
                
                <TextInput styleName="w-full h-10 p-1 font-bold text-xl"
                            onSubmit={this.onTitleChanged} 
                            placeholderText="Title"
                            value={listTitle}/>
              </div>

              <TextInput styleName="w-full h-10 m-1 p-1 rounded bg-gray-100 focus:bg-white focus:border-blue-500 focus:border" 
                          placeholderText={`Add task to '${listTitle}', press Enter to save`}
                          clearTextOnSubmit={true}
                          onSubmit={this.onNewTaskSubmit}/>
              <ul>
                  {tasksView}
              </ul>
            </div>
        );
    }

    onTaskSelection = (item: TodoTask)=>{
        this.props.onSelectTask(item);
    }

    onDelete = (item: TodoTask)=>{
      this.props.onDeleteTask(item);
    }

    onToggleIsComplete = (task: TodoTask) => {
      let updateTaskModel: UpdateTask = new UpdateTask(task.id);
      updateTaskModel.isComplete = !task.isComplete;
      this.props.onUpdateTask(updateTaskModel)
    }

    getTaskView(task: TodoTask): React.ReactNode {
        const isSelected: Boolean = task.id === this.props.selectedTaskId;
        const styleName: string = isSelected ? " item-selected" : "";
        return (
          <li key={task.id} className={`item ${styleName} pl-3 pr-1 flex-auto`}>
            <input type="checkbox" checked={task.isComplete} onChange={() => this.onToggleIsComplete(task)} className="ml-2 w-4 h-4 rounded" />
            <span className="ml-1 flex-auto" onClick={() => this.onTaskSelection(task)} >{task.title}</span>
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
                  <button className={`item ${active ? 'item-selected' : ''}`} onClick={()=> this.onDelete(task)}>
                    <span className='px-2'>Delete</span>
                  </button>
                )}
              </Menu.Item>
            </Menu.Items>
          </Menu>
        );
      }
}