import TodoTaskSession from "./TodoTaskSession";

export default class TodoTask {
    id: string;
    title: string;
    description: string = "";
    isComplete: boolean = false;
    listId: string;
    startedSessionTimestamp?: number = undefined;
    sessions: TodoTaskSession[];
    constructor(id: string, title: string, listId: string, description: string = "", isComplete: boolean = false, startedSessionTimestamp: number | null = null, sessions: TodoTaskSession[] = []) {
        this.id = id;
        this.title = title;
        this.listId = listId;
        this.description = description;
        this.isComplete = isComplete;
        this.startedSessionTimestamp = startedSessionTimestamp !== null ? startedSessionTimestamp : undefined;
        this.sessions = sessions;
    }
}