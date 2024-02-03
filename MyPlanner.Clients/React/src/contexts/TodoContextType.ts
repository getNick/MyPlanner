import TodoFolder from "../entities/TodoFolder";
import TodoList from "../entities/TodoList";
import TodoTask from "../entities/TodoTask";
import UpdateFolder from "../entities/UpdateFolder";
import UpdateList from "../entities/UpdateList";
import UpdateTask from "../entities/UpdateTask";

export type TodoContextType = {
  fetchFolders: () => Promise<TodoFolder[] | undefined>;
  fetchFolder: (folderId: string | undefined) => Promise<TodoFolder | undefined>;
  fetchList: (listId: string | undefined) => Promise<TodoList | undefined>;
  fetchTask: (taskId: string | undefined) => Promise<TodoTask | undefined>;

  onAddFolder: (title: string) => void,
  onAddList: (title: string, folderId: string | null) => void,
  onAddTask: (title: string, listId: string) => void;

  onUpdateFolder: (folder: UpdateFolder) => void;
  onUpdateList: (list: UpdateList) => void;
  onUpdateTask: (task: UpdateTask) => void;

  onDeleteFolderOrList: (item: TodoFolder | TodoList) => void;
  onDeleteTask: (task: TodoTask) => void;

  openFoldersIds: string[],
  toggleIsFolderOpen: (folder: TodoFolder) => void;
  openListsIds: string[],
  toggleIsListOpen: (list: TodoList) => void;
}