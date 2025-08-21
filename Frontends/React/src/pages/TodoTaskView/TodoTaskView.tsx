import React, { useEffect, useMemo, useRef, useState } from "react";
import TodoTask from "../../entities/TodoList/TodoTask";
import TodoTaskSession from "../../entities/TodoList/TodoTaskSession";
import UpdateTask from "../../entities/TodoList/UpdateTask";
import TextInput from "../../components/TextInput/TextInput";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faChevronLeft } from "@fortawesome/free-solid-svg-icons";
import { useTodoContext } from "../../contexts/TodoContext";
import { useLoaderData, useNavigate } from "react-router-dom";

import EditorJS, { OutputData } from "@editorjs/editorjs";
// @ts-ignore
import List from "@editorjs/list";
// @ts-ignore
import Checklist from '@editorjs/checklist'
// @ts-ignore
import YoutubeEmbed from 'editorjs-youtube-embed';
// @ts-ignore
import Header from 'editorjs-header-with-alignment';


const TodoTaskView: React.FC = () => {
    const taskId = useLoaderData() as string;
    const navigate = useNavigate();
    const { todoService } = useTodoContext();
    const [task, setTask] = useState<TodoTask | undefined>();
    const editorInstanceRef = useRef<EditorJS | null>(null);

    const sessionsByDay = useMemo(() => {
        if (!task?.sessions) return new Map<string, TodoTaskSession[]>();

        // Group sessions by their start date
        return task.sessions.reduce((acc, session) => {
            if (session.startTimestamp !== undefined) {
                const dateKey = new Date(session.startTimestamp * 1000).toISOString().split('T')[0];
                if (!acc.has(dateKey)) {
                    acc.set(dateKey, []);
                }
                acc.get(dateKey)!.push(session);
            }
            return acc;
        }, new Map<string, TodoTaskSession[]>());
    }, [task?.sessions]);

    const updateTask = async () => {
        const data = await todoService.getTask(taskId);
        setTask(data);
        if (data?.description !== undefined && data?.description !== "") {
            editorInstanceRef.current?.render(JSON.parse(data?.description))
        }
    }

    useEffect(() => {
        initEditor();
        updateTask();
    }, []);

    const initEditor = () => {
        if (!editorInstanceRef.current) {
            const editor = new EditorJS({
                holder: "editorjs",
                placeholder: "Let's take a note!",
                tools: {
                    header: {
                        class: Header,
                        config: {
                            placeholder: 'Enter a header',
                            levels: [2, 3, 4],
                            defaultLevel: 3,
                            defaultAlignment: 'left'
                        }
                    },
                    List,
                    Checklist,
                    youtubeEmbed: YoutubeEmbed,
                }
            })
            editorInstanceRef.current = editor;
        }
    }

    const onTitleChanged = async (newTitle: string) => {
        let changeTitleChange: UpdateTask = new UpdateTask(taskId);
        changeTitleChange.title = newTitle;
        await todoService.updateTask(changeTitleChange);
    }
    const onDescriptionChanged = async () => {
        const descriptionData: OutputData | undefined = await editorInstanceRef.current?.save();
        let changeDescriptionChange: UpdateTask = new UpdateTask(taskId);
        changeDescriptionChange.description = JSON.stringify(descriptionData);;
        await todoService.updateTask(changeDescriptionChange);
    }

    const navigateBack = () => {
        navigate(-1);
    }

    return (
        <div className="flex flex-col h-full w-full m-1">
            <div className="flex h-10 items-center">
                <button className="h-8 w-8 rounded hover:bg-slate-200" onClick={navigateBack}>
                    <FontAwesomeIcon icon={faChevronLeft} />
                </button>

                <TextInput styleName="w-full h-10 p-1 font-bold text-xl"
                    onSubmit={onTitleChanged}
                    placeholderText="Title"
                    value={task?.title} />
            </div>

            <div id="editorjs" onBlur={onDescriptionChanged}></div>

            {task?.sessions && task.sessions.length > 0 && (
                <details className="mt-6" open>
                    <summary className="cursor-pointer text-lg font-semibold text-gray-800 hover:text-gray-900">
                        Work Sessions
                    </summary>
                    <div className="mt-2">
                        {Array.from(sessionsByDay.entries())
                            .sort(([dateA], [dateB]) => new Date(dateB).getTime() - new Date(dateA).getTime()) // Sort days, newest first
                            .map(([date, sessionsForDay]) => (
                                <div key={date} className="pt-3 first:pt-2">
                                    <h4 className="text-sm font-medium text-gray-500 mb-1 px-1">
                                        {new Date(date).toLocaleDateString(undefined, { month: 'long', day: 'numeric', year: 'numeric' })}
                                    </h4>
                                    <ul className="">
                                        {sessionsForDay
                                            .sort((a, b) => (b.startTimestamp || 0) - (a.startTimestamp || 0)) // Sort sessions within the day
                                            .map((session: TodoTaskSession) => {
                                                const startTime = session.startTimestamp !== undefined
                                                    ? new Date(session.startTimestamp * 1000).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
                                                    : "Unknown";
                                                const endTime = session.endTimestamp ? new Date(session.endTimestamp * 1000).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : "In progress";

                                                let duration = "N/A";
                                                if (session.endTimestamp && session.startTimestamp !== undefined) {
                                                    const durationMs = (session.endTimestamp - session.startTimestamp) * 1000;
                                                    const hours = Math.floor(durationMs / 3600000);
                                                    const minutes = Math.floor((durationMs % 3600000) / 60000);
                                                    const seconds = Math.floor(((durationMs % 3600000) % 60000) / 1000);
                                                    duration = `${hours.toString().padStart(2, '0')}:${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
                                                }

                                                return (
                                                    <li key={session.id} className="flex items-center py-1.5">
                                                        <p className="text-sm text-gray-800">
                                                            {startTime}
                                                            <span className="text-gray-400 mx-2">→</span>
                                                            {endTime}
                                                        </p>
                                                        <span className="ml-4 inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-800">
                                                            {duration}
                                                        </span>
                                                    </li>
                                                );
                                            })}
                                    </ul>
                                </div>
                            ))}
                    </div>
                </details>
            )}
        </div>
    )
}

export default TodoTaskView;