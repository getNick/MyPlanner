import TodoTask from "./TodoTask";

export default class TodoList{
    id : string;
    title: string;
    tasks: TodoTask[];
    constructor(id: string, title: string, tasks : TodoTask[] = []){
        this.id = id;
        this.title = title;
        this.tasks = tasks;
    }
}