import React, { useState } from "react";
import { TodoContextType } from "./TodoContextType";
import TodoFolder from "../entities/TodoFolder";
import TodoList from "../entities/TodoList";
import TodoService from "../services/TodoService";
import TodoTask from "../entities/TodoTask";
import { json } from "stream/consumers";

export const TodoContext = React.createContext<TodoContextType | null>(null);

const TodoContextProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const todoService = new TodoService();
  const [titleCache] = useState<{ [key: string]: string }>({});
  const [openFoldersIds, setOpenFoldersIds] = useState<Set<string>>(new Set<string>());
  const [openListsIds, setOpenListsIds] = useState<Set<string>>(new Set<string>());

  const updateOpenFoldersIdsFromLocalStorage = async () => {
    const foldersStr = localStorage.getItem("openFoldersIds");
    if (foldersStr !== null) {
      try {
        setOpenFoldersIds(new Set<string>(JSON.parse(foldersStr)));
      } catch (error) {
        setOpenFoldersIds(new Set<string>());
      }
    }
  }

  const updateOpenListIdsFromLocalStorage = async () => {
    const listsStr = localStorage.getItem("openListsIds");
    if (listsStr !== null) {
      try {
        setOpenListsIds(new Set<string>(JSON.parse(listsStr)));
      } catch (error) {
        setOpenListsIds(new Set<string>());
      }
    }
  }

  const fetchFolders = async (): Promise<TodoFolder[] | undefined> => {
    updateOpenFoldersIdsFromLocalStorage();

    const items = await todoService.getFolders();
    updateTitleCache(items);
    return items;
  }

  const fetchFolder = async (folderId: string | undefined): Promise<TodoFolder | undefined> => {
    if (folderId === undefined)
      return undefined;
    updateOpenListIdsFromLocalStorage();

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

  const getCachedTitle = (id: string | undefined): string | undefined => {
    if (id === undefined)
      return undefined;

    if (id in titleCache)
      return titleCache[id];
    return undefined;
  }

  const updateTitleCache = async (folders: TodoFolder[]) => {
    folders.forEach(folder => {
      titleCache[folder.id] = folder.title;
      folder.lists.forEach(list => {
        titleCache[list.id] = list.title;
      })
    });
  }

  const toggleIsFolderOpen = (folder: TodoFolder) => {
    const newSet = new Set(openFoldersIds);
    if (newSet.has(folder.id)) {
      newSet.delete(folder.id);
    } else {
      newSet.add(folder.id);
    }
    setOpenFoldersIds(newSet);
    localStorage.setItem("openFoldersIds", JSON.stringify(Array.from(newSet)));
  }

  const toggleIsListOpen = (list: TodoList) => {
    const newSet = new Set(openListsIds);
    if (newSet.has(list.id)) {
      newSet.delete(list.id);
    } else {
      newSet.add(list.id);
    }
    setOpenListsIds(newSet);
    localStorage.setItem("openListsIds", JSON.stringify(Array.from(newSet)));
  }

  return (
    <TodoContext.Provider value={{
      todoService,
      fetchFolders, fetchFolder, fetchList, fetchTask,
      getCachedTitle,
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