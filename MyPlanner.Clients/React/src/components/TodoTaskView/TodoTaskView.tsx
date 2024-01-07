import React from "react";
import TodoTask from "../../entities/TodoTask";
import UpdateTask from "../../entities/UpdateTask";
import TextInput from "../TextInput/TextInput";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faChevronLeft } from "@fortawesome/free-solid-svg-icons";

interface TodoTaskViewProps{
    task?: TodoTask | undefined,
    onUpdateTask: (task: UpdateTask) => void,
    closeTaskBar: () => void,
}

interface TodoTaskViewState{

}


export default class TodoTaskView extends React.Component<TodoTaskViewProps,TodoTaskViewState>{
    onTitleChanged = (newTitle: string) => {
        if(this.props.task === undefined)
            return;
        let changeTitleChange: UpdateTask = new UpdateTask(this.props.task.id);
        changeTitleChange.title = newTitle;
        this.props.onUpdateTask(changeTitleChange)
    }
    onDescriptionChanged = (newDescription: string) => {
        if(this.props.task === undefined)
            return;
        let changeDescriptionChange: UpdateTask = new UpdateTask(this.props.task.id);
        changeDescriptionChange.description = newDescription;
        this.props.onUpdateTask(changeDescriptionChange)
    }

    render(): React.ReactNode {
        if(this.props.task === undefined){
            return (
                <div className="flex w-full h-full items-center justify-center">
                    <h3>Click task title to view the detail</h3>
                </div>
            );
        }

        return (
            <div className="flex flex-col h-full w-full m-1">
                <div className="flex h-10 items-center">
                    <button className="h-8 w-8 rounded hover:bg-slate-200" onClick={this.props.closeTaskBar}>
                        <FontAwesomeIcon icon={faChevronLeft} />
                    </button>

                    <TextInput styleName="w-full h-10 p-1 font-bold text-xl"
                        onSubmit={this.onTitleChanged}
                        placeholderText="Title"
                        value={this.props.task.title} />
                </div>

                <TextInput styleName="w-full flex-auto p-1"
                    onSubmit={this.onDescriptionChanged}
                    placeholderText="Description"
                    useTextArea={true}
                    value={this.props.task.description} />
            </div>
        )
    }
}