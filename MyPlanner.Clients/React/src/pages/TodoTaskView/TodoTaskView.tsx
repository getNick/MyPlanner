import React, { useEffect, useRef, useState } from "react";
import TodoTask from "../../entities/TodoTask";
import UpdateTask from "../../entities/UpdateTask";
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
import Embed from '@editorjs/embed';
// @ts-ignore
import Header from 'editorjs-header-with-alignment';


const TodoTaskView: React.FC = () => {
    const taskId = useLoaderData() as string;
    const navigate = useNavigate();
    const { todoService } = useTodoContext();
    const [task, setTask] = useState<TodoTask | undefined>();
    const editorInstanceRef = useRef<EditorJS | null>(null);

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
                    Embed,
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
            {/* <TextInput styleName="w-full flex-auto p-1"
                onSubmit={onDescriptionChanged}
                placeholderText="Description"
                useTextArea={true}
                value={task?.description} /> */}
        </div>
    )
}

export default TodoTaskView;