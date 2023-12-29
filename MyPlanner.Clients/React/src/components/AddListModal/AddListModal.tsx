import React, { useState } from "react";
import { Dialog, Listbox } from "@headlessui/react";
import TodoFolder from "../../entities/TodoFolder";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faPlus, faCheck } from "@fortawesome/free-solid-svg-icons";

interface AddListModalProps{
    isOpen: boolean,
    onAddList: (title: string, folderId: string|null) => void,
    onAddFolder: (title: string) => void,
    onClose:()=> void,
    folders: TodoFolder[],
}
interface AddListModalState{
    newListTitle: string | undefined,
    selectedFolder: FolderItem,
    newFolderTitle: string,
}

class FolderItem{
    id: string;
    name: string;
    constructor(id: string, name: string){
        this.id = id;
        this.name = name;
    }
}

export default class AddListModal extends React.Component<AddListModalProps,AddListModalState>{
    state:AddListModalState ={
        newListTitle : "",
        selectedFolder : new FolderItem("None", "None"),
        newFolderTitle : "",
    }

    onAddList = ()=>{
        if(this.state.newListTitle !== undefined){
            const folderId: string | null = this.state.selectedFolder.id === "None" ? null : this.state.selectedFolder.id;
            this.props.onAddList(this.state.newListTitle, folderId);
        }
            
        this.props.onClose();
    }
    onInput = (e: React.ChangeEvent<HTMLInputElement>) =>{
        this.setState({
            newListTitle: e.target.value,
        });
    }

    onFolderSelection = (value: FolderItem) =>{
        this.setState({
            selectedFolder : value
        });
    }

    onNewFolderNameChanged = (e: React.ChangeEvent<HTMLInputElement>) =>{
        this.setState({
            newFolderTitle : e.target.value,
        })
    }

    onAddFolder = ()=>{
        if(this.state.newFolderTitle.length > 1){
            this.props.onAddFolder(this.state.newFolderTitle);
            this.setState({
                newFolderTitle : "",
            });
        }
    }


    render(): React.ReactNode {
        const foldersList: FolderItem[] = [new FolderItem("None", "None"), ...this.props.folders.map(x=> new FolderItem(x.id,x.title))];
        const selectedFolder = this.state.selectedFolder;

        return (
            <Dialog open={this.props.isOpen} onClose={this.props.onClose} as="div" className="relative z-10">
                <div className="fixed inset-0 bg-black/30" aria-hidden="true" />

                <div className="fixed inset-0 overflow-auto">
                    <div className="flex min-h-full items-center justify-center p-4 text-center">
                        <Dialog.Panel className="w-full max-w-md -translate-y-full rounded-2xl bg-white p-6 text-left align-middle shadow-xl">
                            <Dialog.Title as="h3" className="text-center text-lg font-medium leading-6 text-gray-900">Add List</Dialog.Title>

                            <div className="flex flex-col space-y-6">
                                <input placeholder="Name" onBlur={this.onInput} className="p-2 mt-2 border-gray-200 border-2 rounded"></input>
                                <Listbox value={selectedFolder} onChange={this.onFolderSelection}>
                                    <div className="relative">
                                        <Listbox.Button className="relative w-full cursor-default rounded-lg bg-white py-2 pl-3 pr-10 text-left shadow-md focus:outline-none">
                                            {selectedFolder.name}
                                        </Listbox.Button>
                                        <Listbox.Options className="absolute z-20 mt-1 max-h-60 w-full overflow-auto rounded-md bg-white p-2 text-base shadow-lg ring-1 ring-black/5 focus:outline-none">
                                            {foldersList.map((folder) => this.getOptionContentView(folder))}

                                            <div className="flex space-x-1 h-8">
                                                <input placeholder="Add new folder"
                                                       value={this.state.newFolderTitle}
                                                       onChange={this.onNewFolderNameChanged} 
                                                       className="basis-full pl-1 border-gray-200 border-2 rounded"/>
                                                <button onClick={this.onAddFolder} 
                                                        className="w-20 bg-blue-500 hover:bg-blue-700 text-white font-bold rounded">Add</button>
                                            </div>
                                        </Listbox.Options>
                                    </div>
                                </Listbox>
                                <div className="flex justify-end space-x-2 rounded-b">
                                    <button onClick={this.props.onClose}
                                        className="w-20 bg-transparent hover:bg-gray-200 text-gray-600 font-semibold py-2 px-4 border-2 border-gray-200 rounded">Cancel</button>

                                    <button onClick={this.onAddList}
                                        className="w-20 bg-blue-500 hover:bg-blue-700 text-white font-bold py-2 px-4 rounded">Add</button>
                                </div>
                            </div>

                        </Dialog.Panel>
                    </div>
                </div>
            </Dialog>
        );
    }

    getOptionContentView(folder: FolderItem ): React.ReactNode {
        const isSelected : boolean = folder.id === this.state.selectedFolder.id;
        let className : string = "relative cursor-pointer select-none py-2 px-4";
        if(isSelected)
            className += " bg-blue-100"
        return (
            <Listbox.Option key={folder.id} value={folder} className={className}>
            <div>
                <FontAwesomeIcon icon={faCheck} className="p-1 mr-2" visibility={isSelected ? "visible" : "hidden"}/>
                <span>{folder.name}</span>
            </div> 
            </Listbox.Option>
        );
    }
}