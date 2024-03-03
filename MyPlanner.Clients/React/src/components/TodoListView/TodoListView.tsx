import React, { useEffect, useState } from "react";
import TodoList from "../../entities/TodoList";
import './TodoListView.css'
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faBars, faEllipsis, faChevronLeft } from "@fortawesome/free-solid-svg-icons";
import TodoTask from "../../entities/TodoTask";
import TextInput from "../TextInput/TextInput";
import { Menu } from "@headlessui/react";
import UpdateTask from "../../entities/UpdateTask";
import UpdateList from "../../entities/UpdateList";
import { useTodoContext } from "../../contexts/TodoContext";
import { useLoaderData } from "react-router-dom";
import { useNavigate } from 'react-router-dom';

const TodoListView: React.FC = () => {
  const listId = useLoaderData() as string;
  const navigate = useNavigate();
  const [list, setList] = useState<TodoList | undefined>();

  const { todoService, getCachedTitle } = useTodoContext();

  const updateList = async () => {
    const data = await todoService.getList(listId);
    setList(data);
  }

  useEffect(() => {
    updateList();
  }, []);

  let title = getCachedTitle(listId);
  if (title === undefined)
    title = list?.title;

  const onNewTaskSubmit = async (newTaskTitle: string) => {
    await todoService.createTask(newTaskTitle, listId);
    updateList();
  }

  const onTitleChanged = async (newTitle: string) => {
    let changeTitleChange: UpdateList = new UpdateList(listId);
    changeTitleChange.title = newTitle;
    await todoService.updateList(changeTitleChange);
    updateList();
  }

  const onToggleIsComplete = async (task: TodoTask) => {
    let updateTaskModel: UpdateTask = new UpdateTask(task.id);
    updateTaskModel.isComplete = !task.isComplete;
    await todoService.updateTask(updateTaskModel);
    updateList();
  }

  const onDeleteTask = async (task: TodoTask) => {
    const isRemoved: boolean = await todoService.deleteTask(task.id);
    if (isRemoved) {
      updateList();
    }
  }

  const navigateBack = () => {
    navigate(-1);
  }

  const navigateToTask = (task: TodoTask) => {
    navigate(`task/${task.id}`);
  }

  const getTaskOptions = (task: TodoTask): React.ReactNode => {
    return (
      <Menu as="div" className="relative inline-block text-left">
        <Menu.Button className="h-full w-10 text-zinc-700">
          <FontAwesomeIcon icon={faEllipsis} />
        </Menu.Button>
        <Menu.Items className="absolute z-10 right-0 mt-2 p-2 origin-top-left divide-y divide-gray-100 rounded-md bg-white shadow-lg focus:outline-none">
          <Menu.Item>
            {({ active }) => (
              <button className={`item ${active ? 'item-selected' : ''}`} onClick={() => onDeleteTask(task)}>
                <span className='px-2'>Delete</span>
              </button>
            )}
          </Menu.Item>
        </Menu.Items>
      </Menu>
    );
  }

  const getTaskView = (task: TodoTask): React.ReactNode => {
    const isSelected: Boolean = false;
    const styleName: string = isSelected ? " item-selected" : "";
    const completeTaskStyle: string = task.isComplete ? "line-through" : "";
    return (
      <li key={task.id} className={`item ${styleName} pl-3 pr-1 flex-auto`}>
        <input type="checkbox" checked={task.isComplete} onChange={() => onToggleIsComplete(task)} className="ml-2 w-4 h-4 rounded" />
        <span className={`${completeTaskStyle} ml-1 flex-auto`} onClick={() => navigateToTask(task)} >{task.title}</span>
        {getTaskOptions(task)}
      </li>
    );
  }

  const doneTasks: TodoTask[] = list?.tasks.filter((task) => task.isComplete === true) ?? [];
  const undoneTasks: TodoTask[] = list?.tasks.filter((task) => task.isComplete === false) ?? [];
  const tasksView: React.ReactNode = undoneTasks.map((task) => getTaskView(task));
  const doneTasksView: React.ReactNode = doneTasks.map((task) => getTaskView(task));
  return (
    <div className="m-1">
      <div className="flex h-10 items-center">
        <button className="h-8 w-8 rounded hover:bg-slate-200" onClick={navigateBack}>
          <FontAwesomeIcon icon={faChevronLeft} />
        </button>

        <TextInput styleName="w-full h-10 p-1 font-bold text-xl"
          onSubmit={onTitleChanged}
          placeholderText="Title"
          value={title} />
      </div>

      <TextInput styleName="w-full h-10 m-1 p-1 rounded bg-gray-100 focus:bg-white focus:border-blue-500 focus:border"
        placeholderText={`Add task to '${title}', press Enter to save`}
        clearTextOnSubmit={true}
        onSubmit={onNewTaskSubmit} />
      <ul>
        {tasksView}
      </ul>

      <h3>Completed</h3>
      <ul>
        {doneTasksView}
      </ul>

    </div>
  );
}

export default TodoListView;