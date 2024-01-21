import React from 'react';
import './App.css';
import FoldersList from '../FoldersList/FoldersList';
import TodoList from '../../entities/TodoList';
import TodoListView from '../TodoListView/TodoListView';
import TodoTaskView from '../TodoTaskView/TodoTaskView';
import TodoFolderView from '../TodoFolderView/TodoFolderView';
import { useTodoContext } from '../../contexts/TodoContext';

const App : React.FC = () => {
  const { 
    selectedFolderOrList, selectedTask,
    isSidebarOpen, isTaskbarOpen} = useTodoContext();

  const isMobile = window.matchMedia(
    "(pointer: coarse) and (hover: none)"
  ).matches;

  const listView = selectedFolderOrList === undefined ? undefined : 
  selectedFolderOrList instanceof TodoList 
  ? (<TodoListView list={selectedFolderOrList} />) 
  : (<TodoFolderView folder={selectedFolderOrList}/>);

  const folderContainerStyle: string = isSidebarOpen ? "" : " -translate-x-full";
  const taskContainerStyle: string = isTaskbarOpen ? "" : "translate-x-full";

  return (
    <div className="container grid md:grid-flow-col h-screen m-0">
        <div className={`fixed top-0 left-0 h-full w-full overflow-auto bg-slate-200 transition-transform transform ${folderContainerStyle} z-10`}>
          <FoldersList />
        </div>

        <div className='h-full w-full overflow-auto p-1'>
          {listView}
        </div>

        <div className={`fixed top-0 left-0 h-full w-full overflow-auto p-1 transition-transform transform ${taskContainerStyle} bg-white`}>
          <TodoTaskView task={selectedTask}/>
        </div>
      </div>
  );
}

export default App;
