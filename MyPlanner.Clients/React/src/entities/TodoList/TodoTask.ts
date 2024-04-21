export default class TodoTask{
    id : string;
    title: string;
    description: string = "";
    isComplete: boolean = false;
    listId : string;
    constructor(id: string, title: string, listId : string, description: string = "", isComplete: boolean = false){
        this.id = id;
        this.title = title;
        this.listId = listId;
        this.description = description;
        this.isComplete = isComplete;
    }
}