import React, { useEffect, useState } from "react";
import TodoList from "../../entities/TodoList/TodoList";
import './TodoListPage.css'
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faChevronLeft } from "@fortawesome/free-solid-svg-icons";
import TodoTask from "../../entities/TodoList/TodoTask";
import UpdateTask from "../../entities/TodoList/UpdateTask";
import { useTodoContext } from "../../contexts/TodoContext";
import { useLoaderData } from "react-router-dom";
import { useNavigate } from 'react-router-dom';
import TextInput from "../../components/TextInput/TextInput";
import TimerControl from "../../components/TimerControl/TimerControl";
import StartTaskSession from "../../entities/TodoList/StartTaskSession";
import StopTaskSession from "../../entities/TodoList/StopTaskSession";

const TodoListPage: React.FC = () => {
  const pageId = useLoaderData() as string;
  const navigate = useNavigate();
  const [list, setList] = useState<TodoList | undefined>();

  const { todoService, getCachedTitle } = useTodoContext();

  const updateList = async () => {
    const data = await todoService.getTodoList(pageId);
    setList(data);
  }

  useEffect(() => {
    updateList();
  }, []);

  let title = getCachedTitle(pageId);

  const onNewTaskSubmit = async (newTaskTitle: string) => {
    if (list === undefined)
      return;

    await todoService.createTask(newTaskTitle, list.id);
    updateList();
  }

  const onTitleChanged = async (newTitle: string) => {
    // let changeTitleChange: UpdateList = new UpdateList(pageId);
    // changeTitleChange.title = newTitle;
    // await todoService.updateList(changeTitleChange);
    // updateList();
  }

  const onToggleIsComplete = async (task: TodoTask) => {
    let updateTaskModel: UpdateTask = new UpdateTask(task.id);
    updateTaskModel.isComplete = !task.isComplete;
    await todoService.updateTask(updateTaskModel);
    updateList();
  }

  const navigateBack = () => {
    navigate(-1);
  }

  const navigateToTask = (task: TodoTask) => {
    navigate(`task/${task.id}`);
  }

  const startTaskSession = async (task: TodoTask, timestamp: number) => {
    await todoService.startTaskSession(new StartTaskSession(task.id, timestamp));
  }
  const stopTaskSession = async (task: TodoTask, timestamp: number) => {
    await todoService.stopTaskSession(new StopTaskSession(task.id, timestamp));
  }

  const getTaskView = (task: TodoTask): React.ReactNode => {
    const isSelected: Boolean = false;
    const styleName: string = isSelected ? " item-selected" : "";
    const completeTaskStyle: string = task.isComplete ? "line-through" : "";
    const isRunning: boolean = task.startedSessionTimestamp !== undefined;
    const startTimestamp: number | undefined = task.startedSessionTimestamp;
    return (
      <li key={task.id} className={`item ${styleName} pl-3 pr-1 flex-auto`}>
        <input type="checkbox" checked={task.isComplete} onChange={() => onToggleIsComplete(task)} className="ml-2 w-4 h-4 rounded" />
        <span className={`${completeTaskStyle} ml-1 flex-auto`} onClick={() => navigateToTask(task)} >{task.title}</span>
        {!task.isComplete &&
          <TimerControl
            isRunning={isRunning}
            startTimestamp={startTimestamp}
            onStart={(timestamp) => startTaskSession(task, timestamp)}
            onStop={(timestamp) => stopTaskSession(task, timestamp)}
          />}
      </li>
    );
  }

  const getUndoneTasksView = (tasks: TodoTask[]): React.ReactNode => {
    const undoneTasks: TodoTask[] = tasks.filter((task) => task.isComplete === false) ?? [];
    if (undoneTasks.length === 0)
      return undefined;

    const tasksView: React.ReactNode = undoneTasks.map((task) => getTaskView(task));
    return (
      <ul>
        {tasksView}
      </ul>
    )
  }

  const getDoneTasksView = (tasks: TodoTask[]): React.ReactNode => {
    const doneTasks: TodoTask[] = tasks.filter((task) => task.isComplete === true) ?? [];
    if (doneTasks.length === 0)
      return undefined;

    const tasksView: React.ReactNode = doneTasks.map((task) => getTaskView(task));
    return (
      <div>
        <h3>Completed</h3>
        <ul>
          {tasksView}
        </ul>
      </div>
    )
  }

  const undoneTaskView = list !== undefined ? getUndoneTasksView(list.tasks) : undefined;
  const doneTaskView = list !== undefined ? getDoneTasksView(list.tasks) : undefined;
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

      {undoneTaskView}
      {doneTaskView}
    </div>
  );
}

export default TodoListPage;