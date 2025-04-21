export default class StartTaskSession {
    taskId: string;
    timestamp: number;
    constructor(taskId: string, timestamp: number) {
        this.taskId = taskId;
        this.timestamp = timestamp;
    }
}