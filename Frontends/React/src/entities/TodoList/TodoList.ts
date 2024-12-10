import TodoTask from "./TodoTask";

export default class TodoList {
    id: string;
    tasks: TodoTask[];
    constructor(id: string, tasks: TodoTask[] = []) {
        this.id = id;
        this.tasks = tasks;
    }
}