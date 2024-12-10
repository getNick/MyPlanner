import { PageType } from "./PageType";

export default class CreatePage {
    title: string;
    userId: string;
    pageType: PageType;
    parentPageId?: string;
    constructor(title: string, userId: string, pageType: PageType = "Folder", parentPageId: string | undefined = undefined) {
        this.title = title;
        this.userId = userId;
        this.pageType = pageType;
        this.parentPageId = parentPageId;
    }
}