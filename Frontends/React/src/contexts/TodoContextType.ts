import Page from "../entities/Pages/Page";
import TodoList from "../entities/TodoList/TodoList";
import TodoTask from "../entities/TodoList/TodoTask";
import User from "../entities/User";
import TodoService from "../services/TodoService";

export type TodoContextType = {
  todoService: TodoService;
  user: User | undefined;

  login: (accessToken: string) => void;
  logout: () => void;
  isLoggedIn: () => boolean;

  fetchPages: () => Promise<Page[] | undefined>;
  fetchPage: (pageId: string | undefined) => Promise<Page | undefined>;

  getCachedTitle: (id: string | undefined) => string | undefined;

  openListsIds: Set<string>;
  toggleIsListOpen: (list: TodoList) => void;
  openFoldersIds: Set<string>;
  toggleIsFolderOpen: (folder: Page) => void;
}