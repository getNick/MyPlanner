export default class TodoTask{
    id : string;
    title: string;
    description: string = "";
    isComplete: boolean = false;
    constructor(id: string, title: string, description: string = "", isComplete: boolean = false){
        this.id = id;
        this.title = title;
        this.description = description;
        this.isComplete = isComplete;
    }
}