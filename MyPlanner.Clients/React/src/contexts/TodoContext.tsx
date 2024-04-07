import React, { useState } from "react";
import { TodoContextType } from "./TodoContextType";
import Page from "../entities/Pages/Page";
import TodoList from "../entities/TodoList";
import TodoService from "../services/TodoService";
import TodoTask from "../entities/TodoTask";
import User from "../entities/User";
import { googleLogout } from '@react-oauth/google';
import { jwtDecode, JwtPayload } from 'jwt-decode'
export const TodoContext = React.createContext<TodoContextType | null>(null);

const TodoContextProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const todoService = new TodoService();
  const [titleCache] = useState<{ [key: string]: string }>({});
  const [openFoldersIds, setOpenFoldersIds] = useState<Set<string>>(new Set<string>());
  const [openListsIds, setOpenListsIds] = useState<Set<string>>(new Set<string>());

  const getUserInfo = (accessToken: string | null): User | undefined => {
    if (accessToken === null || accessToken === "")
      return undefined;
    const decoded: JwtPayload | any = jwtDecode(accessToken);
    const user: User = new User(decoded.sub);
    user.firstName = decoded?.given_name;
    user.lastName = decoded?.family_name;
    user.email = decoded?.email;
    user.picture = decoded?.picture;
    return user;
  }

  const userAccessToken = localStorage.getItem("user_access_token");
  const [user, setUser] = useState<User | undefined>(getUserInfo(userAccessToken));

  const login = async (accessToken: string) => {
    localStorage.setItem("user_access_token", accessToken);
    setUser(getUserInfo(accessToken));
  }

  const logout = async () => {
    googleLogout();
    localStorage.setItem("user_access_token", "");
    setUser(undefined);
  }

  const isLoggedIn = (): boolean => {
    return user !== undefined;
  }

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
    if (user === undefined)
      return undefined;
    updateOpenFoldersIdsFromLocalStorage();
    const items = await todoService.getPages(user.id);
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

  const fetchList = async (listId: string | undefined): Promise<TodoList | undefined> => {
    if (listId === undefined)
      return undefined;

    const data = await todoService.getPageContent(listId);
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
      login, logout, user, isLoggedIn,
      fetchPages, fetchPage, fetchList, fetchTask,
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