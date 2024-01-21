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
import { useTodoContext } from "../../contexts/TodoContext";

interface TodoListViewProps{
  list: TodoList,
}

const TodoListView : React.FC<TodoListViewProps> = (props: TodoListViewProps) => {
  const { 
    selectedTask,
    onSelectTask, 
    onAddTask,
    onDeleteTask,
    onUpdateList, onUpdateTask,
    setIsSidebarOpen} = useTodoContext();

    const onNewTaskSubmit = (newTaskTitle: string) => {
      onAddTask(newTaskTitle, props.list.id);
    }

    const onTitleChanged = (newTitle: string) => {
      let changeTitleChange: UpdateList = new UpdateList(props.list.id);
      changeTitleChange.title = newTitle;
      onUpdateList(changeTitleChange)
  }

  const onToggleIsComplete = (task: TodoTask) => {
    let updateTaskModel: UpdateTask = new UpdateTask(task.id);
    updateTaskModel.isComplete = !task.isComplete;
    onUpdateTask(updateTaskModel);
  }

  const getTaskOptions = (task: TodoTask): React.ReactNode => {
    return (
      <Menu as="div" className="relative inline-block text-left">
        <Menu.Button className="h-full w-10 text-zinc-700">
          <FontAwesomeIcon icon={faEllipsis}/>
        </Menu.Button>
        <Menu.Items className="absolute z-10 right-0 mt-2 p-2 origin-top-left divide-y divide-gray-100 rounded-md bg-white shadow-lg focus:outline-none">
          <Menu.Item>
            {({ active }) => (
              <button className={`item ${active ? 'item-selected' : ''}`} onClick={()=> onDeleteTask(task)}>
                <span className='px-2'>Delete</span>
              </button>
            )}
          </Menu.Item>
        </Menu.Items>
      </Menu>
    );
  }

  const getTaskView = (task: TodoTask): React.ReactNode => {
    const isSelected: Boolean = task.id === selectedTask?.id;
    const styleName: string = isSelected ? " item-selected" : "";
    return (
      <li key={task.id} className={`item ${styleName} pl-3 pr-1 flex-auto`}>
        <input type="checkbox" checked={task.isComplete} onChange={() => onToggleIsComplete(task)} className="ml-2 w-4 h-4 rounded" />
        <span className="ml-1 flex-auto" onClick={() => onSelectTask(task)} >{task.title}</span>
        {getTaskOptions(task)}
      </li>
    );
  }

  const tasksView: React.ReactNode = props.list.tasks.map((folder) => getTaskView(folder));
  const listTitle: string = props.list.title;

  return (
    <div className="m-1">
      <div className="flex h-10 items-center">
        <button className="h-8 w-8 rounded hover:bg-slate-200" onClick={() => setIsSidebarOpen(true)}>
          <FontAwesomeIcon icon={faChevronLeft} />
        </button>

        <TextInput styleName="w-full h-10 p-1 font-bold text-xl"
          onSubmit={onTitleChanged}
          placeholderText="Title"
          value={listTitle} />
      </div>

      <TextInput styleName="w-full h-10 m-1 p-1 rounded bg-gray-100 focus:bg-white focus:border-blue-500 focus:border"
        placeholderText={`Add task to '${listTitle}', press Enter to save`}
        clearTextOnSubmit={true}
        onSubmit={onNewTaskSubmit} />
      <ul>
        {tasksView}
      </ul>
    </div>
  );
}

export default TodoListView;