export default class UpdateNote {
    id: string;
    content: string | undefined;
    constructor(id: string) {
        this.id = id;
    }
}