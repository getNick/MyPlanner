import React from 'react';
import ReactDOM from 'react-dom/client';
import './index.css';
import App from './components/App/App';
import TodoContextProvider from './contexts/TodoContext';
import { GoogleOAuthProvider } from '@react-oauth/google';

const root = ReactDOM.createRoot(
  document.getElementById('root') as HTMLElement
);

root.render(
  <React.StrictMode>
    <GoogleOAuthProvider clientId="925086731832-rv91q8jptt6f8otrviu5tqpi04fa5ljs.apps.googleusercontent.com">
      <TodoContextProvider>
        <App />
      </TodoContextProvider>
    </GoogleOAuthProvider>,
  </React.StrictMode>
);