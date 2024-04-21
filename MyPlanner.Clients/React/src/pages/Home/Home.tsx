import './Home.css'
import React, { FC, useEffect, useState } from 'react';
import Page from '../../entities/Pages/Page';
import TodoList from '../../entities/TodoList/TodoList';
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faPlus, faAngleRight, faAngleDown, faEllipsis, faBars, IconDefinition } from "@fortawesome/free-solid-svg-icons";
import { faFolder, faFolderOpen, faRectangleList, faPenToSquare } from "@fortawesome/free-regular-svg-icons";
import { Menu } from '@headlessui/react';
import { useTodoContext } from '../../contexts/TodoContext';
import { useLoaderData, useNavigate } from 'react-router-dom';
import AddListModal from '../../components/AddListModal/AddListModal';
import { PageType } from '../../entities/Pages/PageType';
import CreatePage from '../../entities/Pages/CreatePage';
import SharePageModal from '../../components/SharePageModal/SharePageModal';
import SharePage from '../../entities/Pages/SharePage';

const Home: React.FC = () => {
  const navigate = useNavigate();
  const { user, logout, fetchPages, todoService, openFoldersIds, toggleIsFolderOpen } = useTodoContext();
  const [pages, setPages] = useState<Page[] | undefined>();
  const [isAddListModalOpen, setIsAddListModalOpen] = useState<boolean>(false);
  const [isSharePageModalOpen, setIsSharePageModalOpen] = useState<boolean>(false);
  const [sharePage, setSharePage] = useState<Page | undefined>();

  const updateFolders = async () => {
    const data = await fetchPages();
    setPages(data);
  }

  useEffect(() => {
    updateFolders();
  }, []);


  const onAddPage = async (title: string, parentPageId: string | undefined, pageType: PageType) => {
    if (user === undefined)
      return;
    const model = new CreatePage(title, user?.id, pageType, parentPageId);
    const listId = await todoService.createPage(model);
    await updateFolders()
  }

  const openSharePage = async (page: Page) => {
    setSharePage(page);
    setIsSharePageModalOpen(true);
  }

  const onSharePage = async (pageId: string, userEmail: string) => {
    todoService.sharePage(new SharePage(pageId, userEmail));
    setIsSharePageModalOpen(false);
  }

  const onDeleteFolderOrList = async (page: Page) => {
    const isRemoved: boolean = await todoService.deletePage(page.id)
    if (isRemoved)
      updateFolders()
  }

  const getIsFolderOpen = (folder: Page): boolean => {
    return openFoldersIds.has(folder.id);
  }

  const navigateToPage = (page: Page) => {
    if (page.content === undefined || page.content === null)
      return;

    if (page.content.type === 'TodoList')
      navigate(`list/${page.id}`)
    else if (page.content.type === 'Note')
      navigate(`note/${page.id}`)
  }

  if (pages === undefined) {
    return (
      <div className="flex flex-col h-full flex-auto m-0 p-1">
        <h3>Loading...</h3>
      </div>)
  }

  const getFolderOptions = (page: Page): React.ReactNode => {
    return (
      <Menu as="div" className="relative inline-block text-left">
        <Menu.Button className="h-full w-10 text-zinc-700">
          <FontAwesomeIcon icon={faEllipsis} />
        </Menu.Button>
        <Menu.Items className="absolute z-20 right-0 mt-2 p-2 origin-top-left divide-y divide-gray-100 rounded-md bg-white shadow-lg focus:outline-none">
          <Menu.Item>
            {({ active }) => (
              <button className={`item ${active ? 'item-selected' : ''}`} onClick={() => openSharePage(page)}>
                <span className='px-2'>Share</span>
              </button>
            )}
          </Menu.Item>
          <Menu.Item>
            {({ active }) => (
              <button className={`item ${active ? 'item-selected' : ''}`} onClick={() => onDeleteFolderOrList(page)}>
                <span className='px-2'>Delete</span>
              </button>
            )}
          </Menu.Item>
        </Menu.Items>
      </Menu>
    );
  }

  const getFolderView = (page: Page): React.ReactNode => {
    const isSelected: Boolean = false;
    const styleName: string = isSelected ? " item-selected" : "";
    const isOpen: boolean = getIsFolderOpen(page);
    const pages: React.ReactNode = isOpen === false ? null :
      (<ul className="w-full mr-1">
        {page.pages?.map((page) => getViewForPage(page))}
      </ul>
      );
    const unfoldIconVisibility: string = page.pages?.length > 0 ? "visible" : "invisible";

    return (
      <li key={page.id}>
        <div className='flex flex-col w-full'>
          <div className={`item ${styleName} flex w-full`}>
            <div className='flex items-center flex-auto'>
              <button className={`h-8 w-8 ${unfoldIconVisibility}`} onClick={(e) => toggleIsFolderOpen(page)}>
                <FontAwesomeIcon icon={isOpen ? faAngleDown : faAngleRight} />
              </button>
              <div className='flex-auto' onClick={(e) => navigateToPage(page)}>
                <FontAwesomeIcon icon={isOpen ? faFolderOpen : faFolder} />
                <span className="ml-1">{page.title}</span>
              </div>
            </div>
            {getFolderOptions(page)}
          </div>
          <div className='ml-8'>
            {pages}
          </div>
        </div>
      </li>
    );
  }

  const getContentPageView = (page: Page): React.ReactNode => {
    if (page.content === undefined)
      return;
    const isSelected: Boolean = false;
    const styleName: string = isSelected ? " item-selected" : "";
    const pageTypeMap: Record<PageType, IconDefinition> = {
      'Note': faPenToSquare,
      'TodoList': faRectangleList,
      'Folder': faFolder,
    };
    const icon: IconDefinition = pageTypeMap[page.content?.type];
    return (
      <li key={page.id}>
        <div className='flex flex-col w-full'>
          <div className={`item ${styleName} flex w-full`}>
            <div className='flex items-center flex-auto'>
              <div className='ml-8 flex-auto' onClick={(e) => navigateToPage(page)}>
                <FontAwesomeIcon icon={icon} />
                <span className="ml-1">{page.title}</span>
              </div>
            </div>
            {getFolderOptions(page)}
          </div>
        </div>
      </li>
    );
  }

  const getViewForPage = (page: Page): React.ReactNode => {
    if (page.content === undefined || page.content === null) {
      return getFolderView(page);
    }
    else {
      return getContentPageView(page);
    }
  }

  const items = pages.map((page) => getViewForPage(page));
  return (
    <div className="flex flex-col h-full flex-auto m-0 p-1">
      <div className="flex p-2 justify-between bg-slate-200 ">
        <div className='flex'>
          <img width={40} src={user?.picture} alt='image'></img>
          <div className='flex flex-col ml-1'>
            <p>{user?.firstName} planer</p>
            <p className=''>{user?.email}</p>
          </div>
        </div>
        <button onClick={logout} className='bg-slate-300 p-2 rounded'>Logout</button>
      </div>
      <div className="flex justify-between p-1">
        <span>Lists</span>
        <button className="h-5 w-5 bg-slate-300 text-zinc-700 rounded" onClick={() => setIsAddListModalOpen(true)}>
          <FontAwesomeIcon icon={faPlus} />
        </button>
        <AddListModal isOpen={isAddListModalOpen}
          folders={pages}
          preselectedFolder={undefined}
          onAddPage={onAddPage}
          onClose={() => setIsAddListModalOpen(false)} />
      </div>

      <ul>
        {items}
      </ul>

      <SharePageModal isOpen={isSharePageModalOpen}
        Page={sharePage}
        onShare={onSharePage}
        onClose={() => setIsSharePageModalOpen(false)} />
    </div>)
}

export default Home;