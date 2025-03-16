import React, { } from 'react';
import { SignIn as ClerkSignIn } from "@clerk/clerk-react";


const SignIn: React.FC = () => {
  return (
    <div className="flex items-center justify-center min-h-screen bg-gray-100">
      <ClerkSignIn />
    </div>
  )
}

export default SignIn;