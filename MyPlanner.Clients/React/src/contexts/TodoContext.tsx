import React, { useEffect, useState } from "react";
import { TodoContextType } from "./TodoContextType";
import TodoFolder from "../entities/TodoFolder";
import TodoList from "../entities/TodoList";
import TodoService from "../services/TodoService";
import TodoTask from "../entities/TodoTask";
import UpdateFolder from "../entities/UpdateFolder";
import UpdateList from "../entities/UpdateList";
import UpdateTask from "../entities/UpdateTask";

export const TodoContext = React.createContext<TodoContextType | null>(null);

const TodoContextProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const todoService = new TodoService();
  const [folders, setFolders] = useState<TodoFolder[] | undefined>();
  const [openFoldersIds, setOpenFoldersIds] = useState<string[]>([]);
  const [openListsIds, setOpenListsIds] = useState<string[]>([]);

  const fetchFolders = async (): Promise<TodoFolder[] | undefined> => {
    const items = await todoService.getFolders();
    return items;
  }

  const fetchFolder = async (folderId: string | undefined): Promise<TodoFolder | undefined> => {
    if (folderId === undefined)
      return undefined;

    const data = await todoService.getFolder(folderId);
    return data;
  }

  const fetchList = async (listId: string | undefined): Promise<TodoList | undefined> => {
    if (listId === undefined)
      return undefined;

    const data = await todoService.getList(listId);
    return data;
  }

  const fetchTask = async (taskId: string | undefined): Promise<TodoTask | undefined> => {
    if (taskId === undefined)
      return undefined;
    const data = await todoService.getTask(taskId);
    return data;
  }

  const updateFolders = async () => {
    const items = await todoService.getFolders();
    setFolders(items);
  }

  const onDeleteFolderOrList = async (item: TodoFolder | TodoList) => {
    const isRemoved: boolean = item instanceof TodoFolder
      ? await todoService.deleteFolder(item.id)
      : await todoService.deleteList(item.id);

    if (isRemoved) {
      const items = await todoService.getFolders();
      setFolders(items);
    }
  }

  const onDeleteTask = async (task: TodoTask) => {
    const isRemoved: boolean = await todoService.deleteTask(task.id);
    if (isRemoved) {
      await updateFolders();
    }
  }

  const onAddList = async (title: string, folderId: string | null) => {
    const listId = await todoService.createList(title, folderId);
    const items = await todoService.getFolders();
    setFolders(items);
  }

  const onAddFolder = async (title: string) => {
    const folderId = await todoService.createFolder(title);
    const items = await todoService.getFolders();
    setFolders(items);
  }

  const onAddTask = async (title: string, listId: string) => {
    const taskId = await todoService.createTask(title, listId);
    await updateFolders();
  }

  const onUpdateFolder = async (folder: UpdateFolder) => {
    const isUpdated = await todoService.updateFolder(folder);
    if (isUpdated === false)
      return;

    await updateFolders();
  }

  const onUpdateList = async (list: UpdateList) => {
    const isUpdated = await todoService.updateList(list);
    if (isUpdated === false)
      return;

    await updateFolders();
  }

  const onUpdateTask = async (task: UpdateTask) => {
    const isUpdated = await todoService.updateTask(task);
    if (isUpdated === false)
      return;

    await updateFolders();
  }

  const toggleIsFolderOpen = (folder: TodoFolder) => {
    const newFoldersIds: string[] = openFoldersIds.includes(folder.id)
      ? openFoldersIds.filter((item) => item !== folder.id) // exclude
      : [...openFoldersIds, folder.id]; // add
    setOpenFoldersIds(newFoldersIds);
  }

  const toggleIsListOpen = (list: TodoList) => {
    const newListsIds: string[] = openListsIds.includes(list.id)
      ? openListsIds.filter((item) => item !== list.id) // exclude
      : [...openListsIds, list.id]; // add
    setOpenListsIds(newListsIds);
  }

  return (
    <TodoContext.Provider value={{
      fetchFolders, fetchFolder, fetchList, fetchTask,
      onAddFolder, onAddList, onAddTask,
      onUpdateFolder, onUpdateList, onUpdateTask,
      onDeleteFolderOrList, onDeleteTask,
      openFoldersIds, toggleIsFolderOpen,
      openListsIds, toggleIsListOpen,
    }}>
      {children}
    </TodoContext.Provider>
  );
}

export default TodoContextProvider;

export const useTodoContext = () => {
  const context = React.useContext(TodoContext);
  if (!context) {
    throw new Error('useTodoContext must be used within a TodoProvider');
  }
  return context;
};