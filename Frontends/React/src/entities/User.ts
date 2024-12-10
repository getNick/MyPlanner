export default class User {
    id: string;
    firstName?: string;
    lastName?: string;
    email: string;
    picture?: string;
    constructor(email: string) {
        this.id = email;
        this.email = email;
    }
}