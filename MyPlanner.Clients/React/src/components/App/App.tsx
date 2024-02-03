import React from 'react';
import './App.css';
import FoldersList from '../FoldersList/FoldersList';
import TodoListView from '../TodoListView/TodoListView';
import TodoTaskView from '../TodoTaskView/TodoTaskView';
import TodoFolderView from '../TodoFolderView/TodoFolderView';
import { RouterProvider, createBrowserRouter, } from 'react-router-dom';
import { useTodoContext } from '../../contexts/TodoContext';

const App: React.FC = () => {
  const {
    fetchFolders,
    fetchFolder,
    fetchList,
    fetchTask
  } = useTodoContext();

  const router = createBrowserRouter([
    {
      path: "/",
      element: <FoldersList />,
      loader: async () => {
        return await fetchFolders();
      },
    },
    {
      path: "folder/:folderId",
      loader: async ({ params }) => {
        return await fetchFolder(params.folderId);
      },
      element: <TodoFolderView />
    },
    {
      path: "list/:listId",
      loader: async ({ params }) => {
        return await fetchList(params.listId);
      },
      element: <TodoListView />
    },
    {
      path: "folder/:folderId/task/:taskId",
      loader: async ({ params }) => {
        return await fetchTask(params.taskId);
      },
      element: <TodoTaskView />
    },
    {
      path: "list/:listId/task/:taskId",
      loader: async ({ params }) => {
        return await fetchTask(params.taskId);
      },
      element: <TodoTaskView />
    },
  ]);

  return (
    <div className="container grid md:grid-flow-col h-screen m-0">
      <RouterProvider router={router} fallbackElement={<p>Loading...</p>} />
    </div>);
}

export default App;
