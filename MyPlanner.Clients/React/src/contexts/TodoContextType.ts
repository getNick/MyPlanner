import TodoFolder from "../entities/TodoFolder";
import TodoList from "../entities/TodoList";
import TodoTask from "../entities/TodoTask";
import UpdateFolder from "../entities/UpdateFolder";
import UpdateList from "../entities/UpdateList";
import UpdateTask from "../entities/UpdateTask";

export type TodoContextType = {
  folders: TodoFolder[] | undefined,
  selectedFolderOrList: TodoFolder | TodoList | undefined,
  selectedTask: TodoTask | undefined,

  onFolderOrListSelection : (item: TodoFolder | TodoList) => void;
  onSelectTask : (task: TodoTask) => void;
  onSelectTaskById : (taskId: string) => void;

  onAddFolder : (title: string) => void,
  onAddList : (title: string, folderId: string|null) => void,
  onAddTask : (title: string, listId: string) => void;

  onUpdateFolder : (folder: UpdateFolder) => void;
  onUpdateList : (list: UpdateList) => void;
  onUpdateTask : (task: UpdateTask) => void;

  onDeleteFolderOrList: (item: TodoFolder | TodoList) => void;
  onDeleteTask : (task: TodoTask) => void;

  openFoldersIds: string[],
  toggleIsFolderOpen : (folder: TodoFolder) => void;
  openListsIds: string[],
  toggleIsListOpen : (list: TodoList) => void;

  isSidebarOpen : boolean,
  setIsSidebarOpen : (isOpen: boolean) => void;
  isTaskbarOpen : boolean,
  setIsTaskbarOpen : (isOpen: boolean) => void;
}