import React from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '@clerk/clerk-react';

const ProtectedRoute: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const { isSignedIn } = useAuth();

    if (isSignedIn === undefined) {
        // Optionally, you can show a loading indicator while the auth state is being determined
        return <p>Loading...</p>;
    }

    if (!isSignedIn) {
        return <Navigate to="/login" />;
    }

    return <>{children}</>;
};

export default ProtectedRoute;