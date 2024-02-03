import React, { useEffect, useState } from "react";
import TodoTask from "../../entities/TodoTask";
import UpdateTask from "../../entities/UpdateTask";
import TextInput from "../TextInput/TextInput";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faChevronLeft } from "@fortawesome/free-solid-svg-icons";
import { useTodoContext } from "../../contexts/TodoContext";
import { useLoaderData, useNavigate } from "react-router-dom";

const TodoTaskView: React.FC = () => {
    const task = useLoaderData() as TodoTask | undefined;
    const navigate = useNavigate();

    const { onUpdateTask } = useTodoContext();


    const onTitleChanged = (newTitle: string) => {
        if (task === undefined)
            return;
        let changeTitleChange: UpdateTask = new UpdateTask(task.id);
        changeTitleChange.title = newTitle;
        onUpdateTask(changeTitleChange)
    }
    const onDescriptionChanged = (newDescription: string) => {
        if (task === undefined)
            return;
        let changeDescriptionChange: UpdateTask = new UpdateTask(task.id);
        changeDescriptionChange.description = newDescription;
        onUpdateTask(changeDescriptionChange)
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

            <TextInput styleName="w-full flex-auto p-1"
                onSubmit={onDescriptionChanged}
                placeholderText="Description"
                useTextArea={true}
                value={task?.description} />
        </div>
    )
}

export default TodoTaskView;