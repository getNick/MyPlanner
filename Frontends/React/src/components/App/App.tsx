import React from 'react';
import './App.css';
import TodoTaskView from '../../pages/TodoTaskView/TodoTaskView';
import { RouterProvider, createBrowserRouter, Navigate } from 'react-router-dom';
import SignIn from '../../pages/SignIn/SignIn';
import Home from '../../pages/Home/Home';
import TodoListPage from '../../pages/TodoListPage/TodoListPage';
import NotePage from '../../pages/NotePage/NotePage';
import ProtectedRoute from '../ProtectedRoute/ProtectedRoute';

const App: React.FC = () => {

  const router = createBrowserRouter([
    {
      path: "login",
      element: <SignIn />
    },
    {
      path: "/",
      element: (
        <ProtectedRoute>
          <Home />
        </ProtectedRoute>
      ),
    },
    {
      path: "list/:listId",
      loader: async ({ params }) => {
        return params.listId;
      },
      element: (
        <ProtectedRoute>
          <TodoListPage />
        </ProtectedRoute>
      ),
    },
    {
      path: "list/:listId/task/:taskId",
      loader: async ({ params }) => {
        return params.taskId;
      },
      element: (
        <ProtectedRoute>
          <TodoTaskView />
        </ProtectedRoute>
      ),
    },
    {
      path: "note/:noteId",
      loader: async ({ params }) => {
        return params.noteId;
      },
      element: (
        <ProtectedRoute>
          <NotePage />
        </ProtectedRoute>
      ),
    },
  ]);

  return (
    <div className="grid md:grid-flow-col h-screen w-screen m-0">
      <RouterProvider router={router} fallbackElement={<p>Loading...</p>} />
    </div>
  );
}

export default App;
