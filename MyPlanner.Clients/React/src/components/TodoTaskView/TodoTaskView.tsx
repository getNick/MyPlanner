import React from "react";
import TodoTask from "../../entities/TodoTask";
import UpdateTask from "../../entities/UpdateTask";
import TextInput from "../TextInput/TextInput";

interface TodoTaskViewProps{
    task?: TodoTask | undefined,
    onUpdateTask: (task: UpdateTask) => void,
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
            <div className="flex flex-col h-full w-full">
                <TextInput styleName="w-full h-10 p-1 font-bold text-xl"
                            onSubmit={this.onTitleChanged} 
                            placeholderText="Title"
                            value={this.props.task.title}/>
                <TextInput styleName="w-full flex-auto p-1"
                            onSubmit={this.onDescriptionChanged} 
                            placeholderText="Description"
                            useTextArea={true}
                            value={this.props.task.description}/>
            </div>
        )
    }
}