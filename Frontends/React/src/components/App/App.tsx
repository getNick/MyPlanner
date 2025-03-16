import React from 'react';
import './App.css';
import TodoTaskView from '../../pages/TodoTaskView/TodoTaskView';
import { RouterProvider, createBrowserRouter, defer, Navigate, } from 'react-router-dom';
import { useTodoContext } from '../../contexts/TodoContext';
import SignIn from '../../pages/SignIn/SignIn';
import Home from '../../pages/Home/Home';
import TodoListPage from '../../pages/TodoListPage/TodoListPage';
import NotePage from '../../pages/NotePage/NotePage';
import { useAuth } from '@clerk/clerk-react';

const App: React.FC = () => {
  const todoContext = useTodoContext();
  const { isSignedIn } = useAuth()

  const privatePage = (page: any) => {
    if (isSignedIn === false) {
      console.log("navigate to login")
      return <Navigate to={"login"} />
    }
    return page;
  }

  const router = createBrowserRouter([
    {
      path: "login",
      element: <SignIn />
    },
    {
      path: "/",
      element: privatePage(<Home />),
    },
    {
      path: "list/:listId",
      loader: async ({ params }) => {
        return params.listId;
      },
      element: privatePage(<TodoListPage />)
    },
    {
      path: "list/:listId/task/:taskId",
      loader: async ({ params }) => {
        return params.taskId;
      },
      element: privatePage(<TodoTaskView />)
    },
    {
      path: "note/:noteId",
      loader: async ({ params }) => {
        return params.noteId;
      },
      element: privatePage(<NotePage />)
    },
  ]);

  return (
    <div className="grid md:grid-flow-col h-screen w-screen m-0">
      <RouterProvider router={router} fallbackElement={<p>Loading...</p>} />
    </div>);
}

export default App;
