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
  const [selectedFolderOrList, setSelectedFolderOrList] = useState<TodoFolder | TodoList | undefined>();
  const [selectedTask, setSelectedTask] = useState<TodoTask>();
  const [openFoldersIds, setOpenFoldersIds] = useState<string[]>([]);
  const [openListsIds, setOpenListsIds] = useState<string[]>([]);
  const [isSidebarOpen, setIsSidebarOpen] = useState<boolean>(true);
  const [isTaskbarOpen, setIsTaskbarOpen] = useState<boolean>(false);

  useEffect(() => {
    const fetch = async () => {
      const items = await todoService.getFolders();
      setFolders(items);
    }
    fetch();
  }, []);


  const onFolderOrListSelection = async (item: TodoFolder | TodoList) => {
    const selectedItem: TodoFolder | TodoList | undefined = item instanceof TodoFolder
      ? await todoService.getFolder(item.id)
      : await todoService.getList(item.id);

    setIsSidebarOpen(false);
    setSelectedFolderOrList(selectedItem);
    setSelectedTask(undefined);
  }

  const updateSelectedFolderOfList = async () => {
    if(selectedFolderOrList instanceof TodoList){
      const selectedList = await todoService.getList(selectedFolderOrList.id);
      setSelectedFolderOrList(selectedList);
    }
    else if(selectedFolderOrList instanceof TodoFolder){
      const selectedFolder = await todoService.getFolder(selectedFolderOrList.id);
      setSelectedFolderOrList(selectedFolder);
    }
  }

  const onSelectTask = (task: TodoTask) => {
    setIsTaskbarOpen(true);
    setSelectedTask(task);
  }

  const onSelectTaskById = async (taskId: string) => {
    const selectedTask: TodoTask | undefined = await todoService.getTask(taskId);
    if (selectedTask !== undefined)
      setSelectedTask(selectedTask);
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

  const onDeleteTask = async (task: TodoTask) =>{
    const isRemoved: boolean = await todoService.deleteTask(task.id);
    if(isRemoved){
      await updateSelectedFolderOfList();
    }
  }

  const onAddList = async (title: string, folderId: string | null) => {
    const listId = await todoService.createList(title, folderId);
    const items = await todoService.getFolders();
    const selectedItem = await todoService.getList(listId);
    setFolders(items);
    setSelectedFolderOrList(selectedItem);
  }

  const onAddFolder = async (title: string) => {
    const folderId = await todoService.createFolder(title);
    const items = await todoService.getFolders();
    const selectedItem = await todoService.getFolder(folderId);
    setFolders(items);
    setSelectedFolderOrList(selectedItem);
  }

  const onAddTask = async (title: string, listId: string) =>{
    const taskId = await todoService.createTask(title, listId);
    await updateSelectedFolderOfList();
  }

  const onUpdateFolder = async (folder: UpdateFolder) =>{
    const isUpdated = await todoService.updateFolder(folder);
    if(isUpdated === false)
      return;

    const folderId: string = folder.id;
    const selectedFolderId: string | undefined = selectedFolderOrList instanceof TodoFolder
      ? selectedFolderOrList.id
      : undefined;

    if(selectedFolderId === folderId){
      const selectedList = await todoService.getFolder(folderId);
      setSelectedFolderOrList(selectedList);
    }

    const items = await todoService.getFolders();
    setFolders(items);
  }

  const onUpdateList = async (list: UpdateList) =>{
    const isUpdated = await todoService.updateList(list);
    if(isUpdated === false)
      return;

    const listId: string = list.id;
    const selectedListId: string | undefined = selectedFolderOrList instanceof TodoList
      ? selectedFolderOrList.id
      : undefined;
    if(selectedListId === listId){
      const selectedList = await todoService.getList(listId);
      setSelectedFolderOrList(selectedList);
    }
    const items = await todoService.getFolders();
    setFolders(items);
  }

  const onUpdateTask = async (task: UpdateTask) =>{
    const isUpdated = await todoService.updateTask(task);
    if(isUpdated === false)
      return;

    const selectedTask : TodoTask | undefined = await todoService.getTask(task.id);
    setSelectedTask(selectedTask);
    await updateSelectedFolderOfList();
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
      folders, selectedFolderOrList, selectedTask, 
      onFolderOrListSelection, onSelectTask, onSelectTaskById, 
      onAddFolder, onAddList, onAddTask,
      onUpdateFolder, onUpdateList, onUpdateTask,
      onDeleteFolderOrList, onDeleteTask,
      openFoldersIds, toggleIsFolderOpen,
      openListsIds, toggleIsListOpen,
      isSidebarOpen, setIsSidebarOpen,
      isTaskbarOpen, setIsTaskbarOpen}}>
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