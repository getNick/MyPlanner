import React from "react";
import "./App.css";
import TodoTaskView from "../../pages/TodoTaskView/TodoTaskView";
import {
  RouterProvider,
  createBrowserRouter,
} from "react-router-dom";
import SignIn from "../../pages/SignIn/SignIn";
import Home from "../../pages/Home/Home";
import TodoListPage from "../../pages/TodoListPage/TodoListPage";
import NotePage from "../../pages/NotePage/NotePage";
import ProtectedRoute from "../ProtectedRoute/ProtectedRoute";
import LayoutShell from "../LayoutShell/LayoutShell";
import { TodoTimePage } from "../../pages/TodoTimePage/TodoTimePage";

const App: React.FC = () => {
  const router = createBrowserRouter([
    {
      path: "login",
      element: <SignIn />,
    },
    {
      path: "/",
      element: (
        <ProtectedRoute>
          <LayoutShell />
        </ProtectedRoute>
      ),
      children: [
        {
          index: true,
          element: <Home />,
        },
        {
          path: "todo",
          element: <TodoTimePage />,
        },
        {
          path: "list/:listId",
          loader: async ({ params }) => {
            return params.listId;
          },
          element: <TodoListPage />,
        },
        {
          path: "list/:listId/task/:taskId",
          loader: async ({ params }) => {
            return params.taskId;
          },
          element: <TodoTaskView />,
        },
        {
          path: "note/:noteId",
          loader: async ({ params }) => {
            return params.noteId;
          },
          element: <NotePage />,
        },
      ],
    },
  ]);

  return (
    <RouterProvider router={router} fallbackElement={<p>Loading...</p>} />
  );
};

export default App;
