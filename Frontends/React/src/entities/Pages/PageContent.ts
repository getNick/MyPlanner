import { PageType } from "./PageType";

export default class PageContent {
    id: string;
    type: PageType;
    constructor(id: string, type: PageType) {
        this.id = id;
        this.type = type;
    }
}