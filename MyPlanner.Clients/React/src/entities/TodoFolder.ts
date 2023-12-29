import TodoList from "./TodoList";

export default class TodoFolder{
    id : string;
    title: string;
    lists: TodoList[];
    constructor(id: string, title: string, lists : TodoList[] = []){
        this.id = id;
        this.title = title;
        this.lists = lists;
    }
}