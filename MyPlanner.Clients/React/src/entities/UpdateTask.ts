export default class UpdateTask{
    id : string;
    title: string|undefined;
    description: string|undefined;
    isComplete: boolean|undefined;
    listId : string|undefined;
    constructor(id: string){
        this.id = id;
    }
}