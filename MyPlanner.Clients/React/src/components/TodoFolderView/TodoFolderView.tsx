import React, { useEffect, useState } from "react";
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
import { useLoaderData } from "react-router-dom";
import { useNavigate } from 'react-router-dom';

const TodoFolderView: React.FC = () => {
  const navigate = useNavigate();
  const folderId = useLoaderData() as string;
  const [folder, setFolder] = useState<TodoFolder | undefined>();
  const { todoService, openListsIds, fetchFolder, getCachedTitle, toggleIsListOpen } = useTodoContext();

  const updateFolder = async () => {
    const data = await fetchFolder(folderId);
    setFolder(data);
  }

  useEffect(() => {
    updateFolder();
  }, []);

  let folderName = getCachedTitle(folderId);
  if (folderName === undefined)
    folderName = folder?.title;

  const getIsListOpen = (list: TodoList): boolean => {
    return openListsIds.has(list.id);
  }

  const onNewListSubmit = async (newListTitle: string) => {
    await todoService.createList(newListTitle, folderId);
    updateFolder();
  }

  const onTitleChanged = async (newTitle: string) => {
    let updateFolderModel: UpdateFolder = new UpdateFolder(folderId);
    updateFolderModel.title = newTitle;
    await todoService.updateFolder(updateFolderModel);
    updateFolder();
  }

  const onToggleIsComplete = async (task: TodoTask) => {
    let updateTaskModel: UpdateTask = new UpdateTask(task.id);
    updateTaskModel.isComplete = !task.isComplete;
    await todoService.updateTask(updateTaskModel);
    updateFolder();
  }

  const onDeleteTask = async (task: TodoTask) => {
    const isRemoved: boolean = await todoService.deleteTask(task.id);
    if (isRemoved) {
      await updateFolder();
    }
  }

  const navigateBack = () => {
    navigate(-1);
  }

  const navigateToTask = (task: TodoTask) => {
    navigate(`task/${task.id}`)
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
    const isSelected: Boolean = false;
    const styleName: string = isSelected ? " item-selected" : "";
    return (
      <li key={task.id} className={`item ${styleName} pl-3 pr-1 flex-auto`}>
        <input type="checkbox" checked={task.isComplete} onChange={() => onToggleIsComplete(task)} className="ml-4 w-4 h-4 rounded" />
        <div className="flex-auto" onClick={() => navigateToTask(task)}>
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

  return (
    <div className="m-1">
      <div className="flex h-10 items-center">
        <button className="h-8 w-8 rounded hover:bg-slate-200" onClick={navigateBack}>
          <FontAwesomeIcon icon={faChevronLeft} />
        </button>

        <TextInput styleName="w-full h-10 p-1 font-bold text-xl"
          onSubmit={onTitleChanged}
          placeholderText="Title"
          value={folderName} />
      </div>

      <TextInput styleName="w-full h-10 m-1 p-1 rounded bg-gray-100 focus:bg-white focus:border-blue-500 focus:border"
        placeholderText={`Add list to '${folderName}' folder, press Enter to save`}
        clearTextOnSubmit={true}
        onSubmit={onNewListSubmit} />

      <ul>
        {folder?.lists.map((list) => getListView(list))}
      </ul>
    </div>
  );
}

export default TodoFolderView;