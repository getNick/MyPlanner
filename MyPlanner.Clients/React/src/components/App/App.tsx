import React from 'react';
import './App.css';
import FoldersList from '../FoldersList/FoldersList';
import TodoListView from '../TodoListView/TodoListView';
import TodoTaskView from '../TodoTaskView/TodoTaskView';
import TodoFolderView from '../TodoFolderView/TodoFolderView';
import { RouterProvider, createBrowserRouter, defer, } from 'react-router-dom';
import { useTodoContext } from '../../contexts/TodoContext';

const App: React.FC = () => {
  const router = createBrowserRouter([
    {
      path: "/",
      element: <FoldersList />,
    },
    {
      path: "folder/:folderId",
      loader: async ({ params }) => {
        return params.folderId;
      },
      element: <TodoFolderView />
    },
    {
      path: "list/:listId",
      loader: async ({ params }) => {
        return params.listId;
      },
      element: <TodoListView />
    },
    {
      path: "folder/:folderId/task/:taskId",
      loader: async ({ params }) => {
        return params.taskId;
      },
      element: <TodoTaskView />
    },
    {
      path: "list/:listId/task/:taskId",
      loader: async ({ params }) => {
        return params.taskId;
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
