import React, { FC, useEffect, useState } from 'react';
import { useTodoContext } from '../../contexts/TodoContext';
import { useNavigate } from 'react-router-dom';
import { CredentialResponse, GoogleLogin } from '@react-oauth/google';

const SignIn: React.FC = () => {
  const navigate = useNavigate();
  const todoContext = useTodoContext();

  const responseMessage = (response: CredentialResponse) => {
    todoContext.login(response.credential ?? "")
    navigate("/");
  };

  return (
    <div className="container flex flex-col mx-auto bg-white rounded-lg pt-12 my-5">
      <div className="flex justify-center w-full h-full my-auto xl:gap-14 lg:justify-normal md:gap-5 draggable">
        <div className="flex items-center justify-center w-full lg:p-12">
          <div className="flex items-center xl:p-10">
            <form className="flex flex-col w-full h-full pb-6 text-center bg-white rounded-3xl">
              <h3 className="mb-3 text-4xl font-extrabold text-dark-grey-900">Sign In</h3>
              <GoogleLogin onSuccess={responseMessage} />
            </form>
          </div>
        </div>
      </div>
    </div>)
}

export default SignIn;