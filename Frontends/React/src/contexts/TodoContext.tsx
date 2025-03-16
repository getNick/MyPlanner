import React, { useState } from "react";
import { TodoContextType } from "./TodoContextType";
import Page from "../entities/Pages/Page";
import TodoList from "../entities/TodoList/TodoList";
import TodoService from "../services/TodoService";
import { useAuth } from "@clerk/clerk-react";

export const TodoContext = React.createContext<TodoContextType | null>(null);

const TodoContextProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { getToken } = useAuth();
  const getJwtToken = async (): Promise<string | null> => {
    return await getToken({ template: 'AspNetToken' });
  };

  const todoService = new TodoService(getJwtToken);
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

  const fetchPages = async (): Promise<Page[] | undefined> => {
    updateOpenFoldersIdsFromLocalStorage();
    const items = await todoService.getPages();
    updateTitleCache(items);
    return items;
  }

  const fetchPage = async (pageId: string | undefined): Promise<Page | undefined> => {
    if (pageId === undefined)
      return undefined;
    updateOpenListIdsFromLocalStorage();
    const data = await todoService.getPage(pageId);
    return data;
  }

  const getCachedTitle = (id: string | undefined): string | undefined => {
    if (id === undefined)
      return undefined;

    if (id in titleCache)
      return titleCache[id];
    return undefined;
  }

  const updateTitleCache = async (pages: Page[]) => {
    pages.forEach(page => {
      titleCache[page.id] = page.title;
      updateTitleCache(page.pages)
    });
  }

  const toggleIsFolderOpen = (folder: Page) => {
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
      fetchPages, fetchPage,
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