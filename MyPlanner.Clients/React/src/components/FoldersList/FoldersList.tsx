import './FoldersList.css'
import React, { FC } from 'react';
import TodoFolder from '../../entities/TodoFolder';
import TodoList from '../../entities/TodoList';
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faPlus,faAngleRight, faAngleDown, faEllipsis, faBars} from "@fortawesome/free-solid-svg-icons";
import { faFolder, faFolderOpen } from "@fortawesome/free-regular-svg-icons";
import AddListModal from '../AddListModal/AddListModal';
import { Menu } from '@headlessui/react';

interface FoldersListProps {
    selectedItemId: string | undefined,
    folders: TodoFolder[] | undefined,
    listsWithoutFolder: TodoList[] | undefined,
    onSelection: (item: TodoFolder | TodoList) => void,
    onDelete: (item: TodoFolder | TodoList) => void,
    onAddList: (title: string, folderId: string|null) => void,
    onAddFolder: (title: string) => void,
  }

  interface FoldersListState{
    isAddListModalOpen: boolean,
    openFoldersIds: string[],
  }
  
  export default class ItemList extends React.Component<FoldersListProps,FoldersListState>{
    constructor(props: FoldersListProps){
      super(props);
      this.state = {
        isAddListModalOpen: false,
        openFoldersIds: []
      };
    }

    setIsAddListModalOpen(isOpen: boolean){
      this.setState({
        isAddListModalOpen: isOpen
      })
    }

    onSelection = (item: TodoFolder | TodoList)=>{
      this.props.onSelection(item)
    }

    onDelete = (item: TodoFolder | TodoList)=>{
      this.props.onDelete(item);
    }

    toggleIsFolderOpen = (folder: TodoFolder) => {
      this.setState((state:FoldersListState) => {
        const openFoldersIds: string[] =  state.openFoldersIds.includes(folder.id) 
          ? state.openFoldersIds.filter((item) => item !== folder.id) // exclude
          : [...state.openFoldersIds, folder.id]; // add

          return {openFoldersIds : openFoldersIds}
      });
    }

    getIsFolderOpen = (folder: TodoFolder) : boolean => {
      return this.state.openFoldersIds.includes(folder.id);
    }

    render(): React.ReactNode {
      const folders = this.props.folders;
      if(folders === undefined){
        return (
            <div className="flex flex-col h-full flex-auto m-0 p-1">
            <h3>Loading...</h3>
          </div>)
      }
  
      const foldersView = folders.map((folder) => this.getFolderView(folder));
      const listsWithoutFolder = this.props.listsWithoutFolder?.map((list) => this.getListView(list))
      return (
      <div className="flex flex-col h-full flex-auto m-0 p-1">
        <div className="flex justify-between p-1">
            <span>Lists</span>
            <button className="h-5 w-5 bg-slate-300 text-zinc-700 rounded" onClick={()=> this.setIsAddListModalOpen(true)}>
                <FontAwesomeIcon icon={faPlus}/>
            </button>
            <AddListModal isOpen={this.state.isAddListModalOpen} 
                          folders={folders}
                          onAddList={this.props.onAddList}
                          onAddFolder={this.props.onAddFolder}
                          onClose={()=> this.setIsAddListModalOpen(false)}/>
        </div>

        <ul>
          {foldersView}
          {listsWithoutFolder}
        </ul>
      </div>)
    }

    getFolderView(folder: TodoFolder): React.ReactNode {
      const isSelected: Boolean = folder.id === this.props.selectedItemId;
      const styleName: string = isSelected ? " item-selected" : "";
      const isOpen: boolean = this.getIsFolderOpen(folder);
      const lists: React.ReactNode = isOpen === false ? null : (
        <ul className="w-full mr-1">
          {folder.lists.map((list) => this.getListView(list))}
        </ul> )
      return (
        <li key={folder.id}>
          <div className='flex flex-col w-full'>
            <div className={`item ${styleName} flex w-full`}>
              <div className='flex items-center flex-auto'>
                <button className='h-8 w-8' onClick={(e) => this.toggleIsFolderOpen(folder)}>
                  <FontAwesomeIcon icon={isOpen ? faAngleDown : faAngleRight} />
                </button>
                <div className='flex-auto' onClick={(e) => this.onSelection(folder)}>
                <FontAwesomeIcon icon={isOpen ? faFolderOpen : faFolder} />
                  <span className="ml-1">{folder.title}</span>
                </div>
              </div>
              {this.getFolderOptions(folder)}
            </div>
            {lists}
          </div>
        </li>
      );
    }

    getListView(list: TodoList): React.ReactNode {
      const isSelected: Boolean = list.id === this.props.selectedItemId;
      const styleName: string = isSelected ? " item-selected" : "";
      return (
        <li key={list.id} className={`item ${styleName} pl-3 pr-1 flex-auto`}>
          <FontAwesomeIcon icon={faBars} className='ml-2' />
          <span className="ml-1 flex-auto" onClick={() => this.onSelection(list)}>{list.title}</span>
          {this.getListOptions(list)}
        </li>
      );
    }

    getFolderOptions(folder: TodoFolder): React.ReactNode {
      return (
        <Menu as="div" className="relative inline-block text-left">
          <Menu.Button className="h-full w-10 text-zinc-700">
            <FontAwesomeIcon icon={faEllipsis}/>
          </Menu.Button>
          <Menu.Items className="absolute z-20 right-0 mt-2 p-2 origin-top-left divide-y divide-gray-100 rounded-md bg-white shadow-lg focus:outline-none">
            <Menu.Item>
              {({ active }) => (
                <button className={`item ${active ? 'item-selected' : ''}`} onClick={()=> this.onDelete(folder)}>
                  <span className='px-2'>Delete</span>
                </button>
              )}
            </Menu.Item>
          </Menu.Items>
        </Menu>
      );
    }

    getListOptions(list: TodoList): React.ReactNode {
      return (
        <Menu as="div" className="relative inline-block text-left">
          <Menu.Button className="h-full w-10 text-zinc-700">
            <FontAwesomeIcon icon={faEllipsis}/>
          </Menu.Button>
          <Menu.Items className="absolute z-20 right-0 mt-2 p-2 origin-top-left divide-y divide-gray-100 rounded-md bg-white shadow-lg focus:outline-none">
            <Menu.Item>
              {({ active }) => (
                <button className={`item ${active ? 'item-selected' : ''}`} onClick={()=> this.onDelete(list)}>
                  <span className='px-2'>Delete</span>
                </button>
              )}
            </Menu.Item>
          </Menu.Items>
        </Menu>
      );
    }
  }