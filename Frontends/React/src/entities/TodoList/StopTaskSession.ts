export default class StopTaskSession {
    taskId: string;
    timestamp: number;
    constructor(taskId: string, timestamp: number) {
        this.taskId = taskId;
        this.timestamp = timestamp;
    }
}