import React from 'react';
import ReactDOM from 'react-dom/client';
import './index.css';
import App from './components/App/App';
import TodoContextProvider from './contexts/TodoContext';
import EditorContextProvider from './contexts/EditorContext';
import { ClerkProvider } from '@clerk/clerk-react'

const PUBLISHABLE_KEY = process.env.REACT_APP_CLERK_PUBLISHABLE_KEY;

if (!PUBLISHABLE_KEY) {
  throw new Error('Add your Clerk Publishable Key to the .env file')
}

const root = ReactDOM.createRoot(
  document.getElementById('root') as HTMLElement
);

root.render(
  <React.StrictMode>
    <ClerkProvider publishableKey={PUBLISHABLE_KEY} afterSignOutUrl="/">
      <TodoContextProvider>
        <EditorContextProvider>
          <App />
        </EditorContextProvider>
      </TodoContextProvider>
    </ClerkProvider>
  </React.StrictMode>
);