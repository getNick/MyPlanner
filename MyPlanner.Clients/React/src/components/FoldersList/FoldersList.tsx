import './FoldersList.css'
import React, { FC, useState } from 'react';
import TodoFolder from '../../entities/TodoFolder';
import TodoList from '../../entities/TodoList';
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faPlus, faAngleRight, faAngleDown, faEllipsis, faBars } from "@fortawesome/free-solid-svg-icons";
import { faFolder, faFolderOpen } from "@fortawesome/free-regular-svg-icons";
import AddListModal from '../AddListModal/AddListModal';
import { Menu } from '@headlessui/react';
import { useTodoContext } from '../../contexts/TodoContext';
import { useLoaderData, useNavigate } from 'react-router-dom';


const FoldersList: React.FC = () => {
  const folders = useLoaderData() as TodoFolder[] | undefined;

  const {
    onAddFolder, onAddList,
    onDeleteFolderOrList,
    openFoldersIds, toggleIsFolderOpen } = useTodoContext();

  const navigate = useNavigate();
  const [isAddListModalOpen, setIsAddListModalOpen] = useState<boolean>(false);

  const getIsFolderOpen = (folder: TodoFolder): boolean => {
    return openFoldersIds.includes(folder.id);
  }

  const navigateToFolder = (folder: TodoFolder) => {
    navigate(`folder/${folder.id}`)
  }

  const navigateToList = (list: TodoList) => {
    navigate(`list/${list.id}`)
  }

  if (folders === undefined) {
    return (
      <div className="flex flex-col h-full flex-auto m-0 p-1">
        <h3>Loading...</h3>
      </div>)
  }

  const getFolderOptions = (folder: TodoFolder): React.ReactNode => {
    return (
      <Menu as="div" className="relative inline-block text-left">
        <Menu.Button className="h-full w-10 text-zinc-700">
          <FontAwesomeIcon icon={faEllipsis} />
        </Menu.Button>
        <Menu.Items className="absolute z-20 right-0 mt-2 p-2 origin-top-left divide-y divide-gray-100 rounded-md bg-white shadow-lg focus:outline-none">
          <Menu.Item>
            {({ active }) => (
              <button className={`item ${active ? 'item-selected' : ''}`} onClick={() => onDeleteFolderOrList(folder)}>
                <span className='px-2'>Delete</span>
              </button>
            )}
          </Menu.Item>
        </Menu.Items>
      </Menu>
    );
  }

  const getFolderView = (folder: TodoFolder): React.ReactNode => {
    const isSelected: Boolean = false;
    const styleName: string = isSelected ? " item-selected" : "";
    const isOpen: boolean = getIsFolderOpen(folder);
    const lists: React.ReactNode = isOpen === false ? null : (
      <ul className="w-full mr-1">
        {folder.lists.map((list) => getListView(list))}
      </ul>)
    return (
      <li key={folder.id}>
        <div className='flex flex-col w-full'>
          <div className={`item ${styleName} flex w-full`}>
            <div className='flex items-center flex-auto'>
              <button className='h-8 w-8' onClick={(e) => toggleIsFolderOpen(folder)}>
                <FontAwesomeIcon icon={isOpen ? faAngleDown : faAngleRight} />
              </button>
              <div className='flex-auto' onClick={(e) => navigateToFolder(folder)}>
                <FontAwesomeIcon icon={isOpen ? faFolderOpen : faFolder} />
                <span className="ml-1">{folder.title}</span>
              </div>
            </div>
            {getFolderOptions(folder)}
          </div>
          {lists}
        </div>
      </li>
    );
  }

  const getListOptions = (list: TodoList): React.ReactNode => {
    return (
      <Menu as="div" className="relative inline-block text-left">
        <Menu.Button className="h-full w-10 text-zinc-700">
          <FontAwesomeIcon icon={faEllipsis} />
        </Menu.Button>
        <Menu.Items className="absolute z-20 right-0 mt-2 p-2 origin-top-left divide-y divide-gray-100 rounded-md bg-white shadow-lg focus:outline-none">
          <Menu.Item>
            {({ active }) => (
              <button className={`item ${active ? 'item-selected' : ''}`} onClick={() => onDeleteFolderOrList(list)}>
                <span className='px-2'>Delete</span>
              </button>
            )}
          </Menu.Item>
        </Menu.Items>
      </Menu>
    );
  }

  const getListView = (list: TodoList): React.ReactNode => {
    const isSelected: Boolean = false;
    const styleName: string = isSelected ? " item-selected" : "";
    return (
      <li key={list.id} className={`item ${styleName} pl-3 pr-1 flex-auto`}>
        <FontAwesomeIcon icon={faBars} className='ml-2' />
        <span className="ml-1 flex-auto" onClick={() => navigateToList(list)}>{list.title}</span>
        {getListOptions(list)}
      </li>
    );
  }

  const foldersView = folders.map((folder) => getFolderView(folder));
  //const listsWithoutFolder = this.props.listsWithoutFolder?.map((list) => this.getListView(list))
  return (
    <div className="flex flex-col h-full flex-auto m-0 p-1">
      <div className="flex justify-between p-1">
        <span>Lists</span>
        <button className="h-5 w-5 bg-slate-300 text-zinc-700 rounded" onClick={() => setIsAddListModalOpen(true)}>
          <FontAwesomeIcon icon={faPlus} />
        </button>
        <AddListModal isOpen={isAddListModalOpen}
          folders={folders}
          preselectedFolder={undefined}
          onAddList={onAddList}
          onAddFolder={onAddFolder}
          onClose={() => setIsAddListModalOpen(false)} />
      </div>

      <ul>
        {foldersView}
        {/* {listsWithoutFolder} */}
      </ul>
    </div>)
}

export default FoldersList;