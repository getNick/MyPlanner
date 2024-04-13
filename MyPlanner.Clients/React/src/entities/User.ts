export default class User {
    id: string;
    firstName?: string;
    lastName?: string;
    email?: string;
    picture?: string;
    constructor(id: string) {
        this.id = id;
    }
}