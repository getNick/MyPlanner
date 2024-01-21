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
import { useTodoContext } from "../../contexts/TodoContext";


interface TodoFolderViewProps {
  folder: TodoFolder,
}

const TodoFolderView: React.FC<TodoFolderViewProps> = (props: TodoFolderViewProps) => {
  const { folder } = props;

  const {
    selectedTask,
    onSelectTask,
    onAddList,
    onDeleteTask,
    onUpdateFolder, onUpdateTask,
    openListsIds, toggleIsListOpen,
    setIsSidebarOpen} = useTodoContext();

  const onNewListSubmit = (newListTitle: string) => {
    onAddList(newListTitle, folder.id);
  }

  const onTitleChanged = (newTitle: string) => {
    let updateFolderModel: UpdateFolder = new UpdateFolder(folder.id);
    updateFolderModel.title = newTitle;
    onUpdateFolder(updateFolderModel);
  }

  const onToggleIsComplete = (task: TodoTask) => {
    let updateTaskModel: UpdateTask = new UpdateTask(task.id);
    updateTaskModel.isComplete = !task.isComplete;
    onUpdateTask(updateTaskModel);
  }

  const getIsListOpen = (list: TodoList): boolean => {
    return openListsIds.includes(list.id);
  }

  const getTaskOptions = (task: TodoTask): React.ReactNode => {
    return (
      <Menu as="div" className="relative inline-block text-left">
        <Menu.Button className="h-full w-10 text-zinc-700">
          <FontAwesomeIcon icon={faEllipsis} />
        </Menu.Button>
        <Menu.Items className="absolute z-10 right-0 mt-2 p-2 origin-top-left divide-y divide-gray-100 rounded-md bg-white shadow-lg focus:outline-none">
          <Menu.Item>
            {({ active }) => (
              <button className={`item ${active ? 'item-selected' : ''}`} onClick={() => onDeleteTask(task)}>
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
        <input type="checkbox" checked={task.isComplete} onChange={() => onToggleIsComplete(task)} className="ml-4 w-4 h-4 rounded" />
        <div className="flex-auto" onClick={() => onSelectTask(task)}>
          <span className="ml-1">{task.title}</span>
        </div>
        {getTaskOptions(task)}
      </li>
    );
  }

  const getListView = (list: TodoList): React.ReactNode => {
    const isOpen: boolean = getIsListOpen(list);
    const tasks: React.ReactNode = isOpen === false ? null : (
      <ul>
        {list.tasks.map((task) => getTaskView(task))}
      </ul>)

    return (
      <li key={list.id} className={`pl-3 pr-1`}>
        <div>
          <button className='h-8 w-8' onClick={(e) => toggleIsListOpen(list)}>
            <FontAwesomeIcon icon={isOpen ? faAngleDown : faAngleRight} />
          </button>
          <span className="ml-1 font-bold">{list.title}</span>
          <span className="ml-1 text-gray-400">{list.tasks.length}</span>
        </div>
        {tasks}
      </li>
    );
  }

  const listsView: React.ReactNode = folder.lists.map((list) => getListView(list));
  const folderTitle: string = folder.title;

  return (
    <div className="m-1">
      <div className="flex h-10 items-center">
        <button className="h-8 w-8 rounded hover:bg-slate-200" onClick={() => setIsSidebarOpen(true)}>
          <FontAwesomeIcon icon={faChevronLeft} />
        </button>

        <TextInput styleName="w-full h-10 p-1 font-bold text-xl"
          onSubmit={onTitleChanged}
          placeholderText="Title"
          value={folderTitle} />
      </div>

      <TextInput styleName="w-full h-10 m-1 p-1 rounded bg-gray-100 focus:bg-white focus:border-blue-500 focus:border"
        placeholderText={`Add list to '${folderTitle}' folder, press Enter to save`}
        clearTextOnSubmit={true}
        onSubmit={onNewListSubmit} />
      <ul>
        {listsView}
      </ul>
    </div>
  );
}

export default TodoFolderView;