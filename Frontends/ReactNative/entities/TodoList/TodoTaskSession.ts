export default class TodoTaskSession {
    id: string;
    startTimestamp?: number = undefined;
    endTimestamp?: number = undefined;
    constructor(id: string, startTimestamp: number | null = null, endTimestamp: number | null = null) {
        this.id = id;
        this.startTimestamp = startTimestamp !== null ? startTimestamp : undefined;
        this.endTimestamp = endTimestamp !== null ? endTimestamp : undefined;
    }
}