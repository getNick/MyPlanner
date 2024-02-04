import TodoFolder from "../entities/TodoFolder";
import TodoList from "../entities/TodoList";
import TodoTask from "../entities/TodoTask";
import TodoService from "../services/TodoService";

export type TodoContextType = {
  todoService: TodoService;

  fetchFolders: () => Promise<TodoFolder[] | undefined>;
  fetchFolder: (folderId: string | undefined) => Promise<TodoFolder | undefined>;
  fetchList: (listId: string | undefined) => Promise<TodoList | undefined>;
  fetchTask: (taskId: string | undefined) => Promise<TodoTask | undefined>;

  getCachedTitle: (id: string | undefined) => string | undefined;

  openListsIds: Set<string>;
  toggleIsListOpen: (list: TodoList) => void;
  openFoldersIds: Set<string>;
  toggleIsFolderOpen: (folder: TodoFolder) => void;
}