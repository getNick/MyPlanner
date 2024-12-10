import { PageType } from "./PageType";

export default class UpdatePage {
    id: string;
    title: string | undefined;
    pageType: PageType | undefined
    constructor(id: string) {
        this.id = id;
    }
}