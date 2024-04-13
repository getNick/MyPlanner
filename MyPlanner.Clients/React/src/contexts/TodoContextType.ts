import Page from "../entities/Pages/Page";
import TodoList from "../entities/TodoList";
import TodoTask from "../entities/TodoTask";
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
  fetchList: (listId: string | undefined) => Promise<TodoList | undefined>;
  fetchTask: (taskId: string | undefined) => Promise<TodoTask | undefined>;

  getCachedTitle: (id: string | undefined) => string | undefined;

  openListsIds: Set<string>;
  toggleIsListOpen: (list: TodoList) => void;
  openFoldersIds: Set<string>;
  toggleIsFolderOpen: (folder: Page) => void;
}