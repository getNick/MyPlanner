import React, { useState } from "react";
import { Dialog, Listbox } from "@headlessui/react";
import Page from "../../entities/Pages/Page";
import validator from "validator";

interface SharePageModalProps {
    isOpen: boolean,
    Page?: Page,
    onShare: (pageId: string, userEmail: string) => void,
    onClose: () => void,
}

const SharePageModal: React.FC<SharePageModalProps> = (props) => {
    const [userEmail, setUserEmail] = React.useState<string>("");
    const [emailError, setEmailError] = useState("")

    const onShare = () => {
        if (validator.isEmail(userEmail) && props.Page !== undefined) {
            props.onShare(props.Page.id, userEmail);
            props.onClose();
        }
    }

    const onInput = (e: React.ChangeEvent<HTMLInputElement>) => {
        const newValue = e.target.value;
        setUserEmail(newValue);

        if (validator.isEmail(newValue)) {
            setEmailError('')
        } else {
            setEmailError('Invalid Email')
        }
    }

    const shareButtonStyle: string = emailError === ""
        ? "w-20 bg-blue-500 hover:bg-blue-700 text-white font-bold py-2 px-4 rounded"
        : "w-20 bg-gray-300 cursor-not-allowed opacity-50 px-4 py-2 rounded";

    return (
        <Dialog open={props.isOpen} onClose={props.onClose} as="div" className="relative z-10">
            <div className="fixed inset-0 bg-black/30" aria-hidden="true" />

            <div className="fixed inset-0 overflow-auto">
                <div className="flex min-h-full items-center justify-center p-4 text-center">
                    <Dialog.Panel className="w-full max-w-md rounded-2xl bg-white p-6 text-left align-middle shadow-xl">
                        <Dialog.Title as="h3" className="text-center text-lg font-medium leading-6 text-gray-900">Share page "{props.Page?.title}"</Dialog.Title>

                        <div className="flex flex-col space-y-6">
                            <input placeholder="Enter email" onInput={onInput} className="p-2 mt-2 border-gray-200 border-2 rounded"></input>
                            <span style={{ fontWeight: 'bold', color: 'red' }}>{emailError}</span>
                            <div className="flex justify-end space-x-2 rounded-b">
                                <button onClick={props.onClose}
                                    className="w-20 bg-transparent hover:bg-gray-200 text-gray-600 font-semibold py-2 px-4 border-2 border-gray-200 rounded">Cancel</button>

                                <button onClick={onShare} className={shareButtonStyle}>Share</button>
                            </div>
                        </div>
                    </Dialog.Panel>
                </div>
            </div>
        </Dialog>
    );
}

export default SharePageModal;